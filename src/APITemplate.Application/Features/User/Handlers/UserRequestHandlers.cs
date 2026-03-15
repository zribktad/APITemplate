using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.User.Mappings;
using APITemplate.Application.Features.User.Specifications;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace APITemplate.Application.Features.User;

public sealed record GetUsersQuery(UserFilter Filter) : IRequest<PagedResponse<UserResponse>>;

public sealed record GetUserByIdQuery(Guid Id) : IRequest<UserResponse?>;

public sealed record CreateUserCommand(CreateUserRequest Request) : IRequest<UserResponse>;

public sealed record UpdateUserCommand(Guid Id, UpdateUserRequest Request) : IRequest;

public sealed record ActivateUserCommand(Guid Id) : IRequest;

public sealed record DeactivateUserCommand(Guid Id) : IRequest;

public sealed record ChangeUserRoleCommand(Guid Id, ChangeUserRoleRequest Request) : IRequest;

public sealed record DeleteUserCommand(Guid Id) : IRequest;

public sealed class UserRequestHandlers
    : IRequestHandler<GetUsersQuery, PagedResponse<UserResponse>>,
        IRequestHandler<GetUserByIdQuery, UserResponse?>,
        IRequestHandler<CreateUserCommand, UserResponse>,
        IRequestHandler<UpdateUserCommand>,
        IRequestHandler<ActivateUserCommand>,
        IRequestHandler<DeactivateUserCommand>,
        IRequestHandler<ChangeUserRoleCommand>,
        IRequestHandler<DeleteUserCommand>
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly ILogger<UserRequestHandlers> _logger;

    public UserRequestHandlers(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        ILogger<UserRequestHandlers> logger
    )
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<PagedResponse<UserResponse>> Handle(
        GetUsersQuery request,
        CancellationToken ct
    )
    {
        var items = await _repository.ListAsync(new UserFilterSpecification(request.Filter), ct);
        var totalCount = await _repository.CountAsync(
            new UserCountSpecification(request.Filter),
            ct
        );
        return new PagedResponse<UserResponse>(
            items,
            totalCount,
            request.Filter.PageNumber,
            request.Filter.PageSize
        );
    }

    public async Task<UserResponse?> Handle(GetUserByIdQuery request, CancellationToken ct) =>
        await _repository.FirstOrDefaultAsync(new UserByIdSpecification(request.Id), ct);

    public async Task<UserResponse> Handle(CreateUserCommand command, CancellationToken ct)
    {
        await ValidateEmailUniqueAsync(command.Request.Email, ct);
        await ValidateUsernameUniqueAsync(command.Request.Username, ct);

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = command.Request.Username,
            Email = command.Request.Email,
            PasswordHash = _passwordHasher.Hash(command.Request.Password),
        };

        await _repository.AddAsync(user, ct);
        await _unitOfWork.CommitAsync(ct);

        try
        {
            await _publisher.Publish(
                new UserRegisteredNotification(user.Id, user.Email, user.Username),
                ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to publish UserRegisteredNotification for user {UserId}.", user.Id);
        }

        return user.ToResponse();
    }

    public async Task Handle(UpdateUserCommand command, CancellationToken ct)
    {
        var user = await GetUserOrThrowAsync(command.Id, ct);

        if (!string.Equals(user.Email, command.Request.Email, StringComparison.OrdinalIgnoreCase))
            await ValidateEmailUniqueAsync(command.Request.Email, ct);

        var normalizedNew = AppUser.NormalizeUsername(command.Request.Username);
        if (!string.Equals(user.NormalizedUsername, normalizedNew, StringComparison.Ordinal))
            await ValidateUsernameUniqueAsync(command.Request.Username, ct);

        user.Username = command.Request.Username;
        user.Email = command.Request.Email;

        await _repository.UpdateAsync(user, ct);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task Handle(ActivateUserCommand command, CancellationToken ct) =>
        await UpdateUserAsync(command.Id, user => user.IsActive = true, ct);

    public async Task Handle(DeactivateUserCommand command, CancellationToken ct) =>
        await UpdateUserAsync(command.Id, user => user.IsActive = false, ct);

    public async Task Handle(ChangeUserRoleCommand command, CancellationToken ct)
    {
        var user = await GetUserOrThrowAsync(command.Id, ct);
        var oldRole = user.Role.ToString();

        user.Role = command.Request.Role;
        await _repository.UpdateAsync(user, ct);
        await _unitOfWork.CommitAsync(ct);

        try
        {
            await _publisher.Publish(
                new UserRoleChangedNotification(
                    user.Id,
                    user.Email,
                    user.Username,
                    oldRole,
                    command.Request.Role.ToString()
                ),
                ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to publish UserRoleChangedNotification for user {UserId}.", user.Id);
        }
    }

    public async Task Handle(DeleteUserCommand command, CancellationToken ct)
    {
        var user = await GetUserOrThrowAsync(command.Id, ct);
        await _repository.DeleteAsync(user, ct);
        await _unitOfWork.CommitAsync(ct);
    }

    private async Task<AppUser> GetUserOrThrowAsync(Guid id, CancellationToken ct)
    {
        return await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(AppUser), id, ErrorCatalog.Users.NotFound);
    }

    private async Task UpdateUserAsync(Guid id, Action<AppUser> applyUpdate, CancellationToken ct)
    {
        var user = await GetUserOrThrowAsync(id, ct);
        applyUpdate(user);
        await _repository.UpdateAsync(user, ct);
        await _unitOfWork.CommitAsync(ct);
    }

    private async Task ValidateEmailUniqueAsync(string email, CancellationToken ct)
    {
        if (await _repository.ExistsByEmailAsync(email, ct))
        {
            throw new ConflictException(
                $"A user with email '{email}' already exists.",
                ErrorCatalog.Users.EmailAlreadyExists
            );
        }
    }

    private async Task ValidateUsernameUniqueAsync(string username, CancellationToken ct)
    {
        var normalized = AppUser.NormalizeUsername(username);
        if (await _repository.ExistsByUsernameAsync(normalized, ct))
        {
            throw new ConflictException(
                $"A user with username '{username}' already exists.",
                ErrorCatalog.Users.UsernameAlreadyExists
            );
        }
    }
}
