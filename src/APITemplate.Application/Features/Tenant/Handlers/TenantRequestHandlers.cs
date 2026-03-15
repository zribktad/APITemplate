using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Features.Tenant.DTOs;
using APITemplate.Application.Features.Tenant.Mappings;
using APITemplate.Application.Features.Tenant.Specifications;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using MediatR;
using TenantEntity = APITemplate.Domain.Entities.Tenant;

namespace APITemplate.Application.Features.Tenant;

public sealed record GetTenantsQuery(TenantFilter Filter) : IRequest<PagedResponse<TenantResponse>>;

public sealed record GetTenantByIdQuery(Guid Id) : IRequest<TenantResponse?>;

public sealed record CreateTenantCommand(CreateTenantRequest Request) : IRequest<TenantResponse>;

public sealed record DeleteTenantCommand(Guid Id) : IRequest;

public sealed class TenantRequestHandlers
    : IRequestHandler<GetTenantsQuery, PagedResponse<TenantResponse>>,
        IRequestHandler<GetTenantByIdQuery, TenantResponse?>,
        IRequestHandler<CreateTenantCommand, TenantResponse>,
        IRequestHandler<DeleteTenantCommand>
{
    private readonly ITenantRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly IActorProvider _actorProvider;
    private readonly TimeProvider _timeProvider;

    public TenantRequestHandlers(
        ITenantRepository repository,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        IActorProvider actorProvider,
        TimeProvider timeProvider
    )
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _actorProvider = actorProvider;
        _timeProvider = timeProvider;
    }

    public async Task<PagedResponse<TenantResponse>> Handle(
        GetTenantsQuery request,
        CancellationToken ct
    )
    {
        var items = await _repository.ListAsync(new TenantSpecification(request.Filter), ct);
        var totalCount = await _repository.CountAsync(
            new TenantCountSpecification(request.Filter),
            ct
        );
        return new PagedResponse<TenantResponse>(
            items,
            totalCount,
            request.Filter.PageNumber,
            request.Filter.PageSize
        );
    }

    public async Task<TenantResponse?> Handle(GetTenantByIdQuery request, CancellationToken ct) =>
        await _repository.FirstOrDefaultAsync(new TenantByIdSpecification(request.Id), ct);

    public async Task<TenantResponse> Handle(CreateTenantCommand command, CancellationToken ct)
    {
        if (await _repository.CodeExistsAsync(command.Request.Code, ct))
            throw new ConflictException(
                $"Tenant with code '{command.Request.Code}' already exists.",
                ErrorCatalog.Tenants.CodeAlreadyExists
            );

        var tenant = await _unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                var id = Guid.NewGuid();
                var entity = new TenantEntity
                {
                    Id = id,
                    TenantId = id,
                    Code = command.Request.Code,
                    Name = command.Request.Name,
                };

                await _repository.AddAsync(entity, ct);
                return entity;
            },
            ct
        );

        await _publisher.Publish(new TenantsChangedNotification(), ct);
        return tenant.ToResponse();
    }

    public async Task Handle(DeleteTenantCommand command, CancellationToken ct)
    {
        await _unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                await _repository.DeleteAsync(command.Id, ct, ErrorCatalog.Tenants.NotFound);
            },
            ct
        );

        await _publisher.Publish(
            new TenantSoftDeletedNotification(
                command.Id,
                _actorProvider.ActorId,
                _timeProvider.GetUtcNow().UtcDateTime
            ),
            ct
        );

        await _publisher.Publish(new TenantsChangedNotification(), ct);
    }
}
