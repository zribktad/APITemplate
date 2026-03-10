using APITemplate.Application.Common.DTOs;
using APITemplate.Application.Common.Errors;
using APITemplate.Application.Features.User.DTOs;
using APITemplate.Application.Features.User.Interfaces;
using APITemplate.Application.Features.User.Mappings;
using APITemplate.Application.Features.User.Specifications;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.User.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public UserService(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _repository.FirstOrDefaultAsync(new UserByIdSpecification(id), ct);
    }

    public async Task<PagedResponse<UserResponse>> GetPagedAsync(UserFilter filter, CancellationToken ct = default)
    {
        var items = await _repository.ListAsync(new UserFilterSpecification(filter), ct);
        var totalCount = await _repository.CountAsync(new UserCountSpecification(filter), ct);

        return new PagedResponse<UserResponse>(items, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        await ValidateEmailUniqueAsync(request.Email, ct);
        await ValidateUsernameUniqueAsync(request.Username, ct);

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password)
        };

        await _repository.AddAsync(user, ct);
        await _unitOfWork.CommitAsync(ct);

        return user.ToResponse();
    }

    public async Task UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await GetUserOrThrowAsync(id, ct);

        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
            await ValidateEmailUniqueAsync(request.Email, ct);

        var normalizedNew = AppUser.NormalizeUsername(request.Username);
        if (!string.Equals(user.NormalizedUsername, normalizedNew, StringComparison.Ordinal))
            await ValidateUsernameUniqueAsync(request.Username, ct);

        user.Username = request.Username;
        user.Email = request.Email;

        await _repository.UpdateAsync(user, ct);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task ActivateAsync(Guid id, CancellationToken ct = default)
        => await UpdateUserAsync(id, user => user.IsActive = true, ct);

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
        => await UpdateUserAsync(id, user => user.IsActive = false, ct);

    public async Task ChangeRoleAsync(Guid id, ChangeUserRoleRequest request, CancellationToken ct = default)
        => await UpdateUserAsync(id, user => user.Role = request.Role, ct);

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await GetUserOrThrowAsync(id, ct);
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
            throw new ConflictException(
                $"A user with email '{email}' already exists.",
                ErrorCatalog.Users.EmailAlreadyExists);
    }

    private async Task ValidateUsernameUniqueAsync(string username, CancellationToken ct)
    {
        var normalized = AppUser.NormalizeUsername(username);
        if (await _repository.ExistsByUsernameAsync(normalized, ct))
            throw new ConflictException(
                $"A user with username '{username}' already exists.",
                ErrorCatalog.Users.UsernameAlreadyExists);
    }
}
