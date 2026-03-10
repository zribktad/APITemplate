using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Resilience;
using APITemplate.Application.Features.ProductData.Mappings;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Polly.Registry;

namespace APITemplate.Application.Features.ProductData;

public sealed record GetProductDataByIdQuery(Guid Id) : IRequest<ProductDataResponse?>;

public sealed record GetProductDataQuery(string? Type) : IRequest<List<ProductDataResponse>>;

public sealed record CreateImageProductDataCommand(CreateImageProductDataRequest Request) : IRequest<ProductDataResponse>;

public sealed record CreateVideoProductDataCommand(CreateVideoProductDataRequest Request) : IRequest<ProductDataResponse>;

public sealed record DeleteProductDataCommand(Guid Id) : IRequest;

public sealed class ProductDataRequestHandlers :
    IRequestHandler<GetProductDataByIdQuery, ProductDataResponse?>,
    IRequestHandler<GetProductDataQuery, List<ProductDataResponse>>,
    IRequestHandler<CreateImageProductDataCommand, ProductDataResponse>,
    IRequestHandler<CreateVideoProductDataCommand, ProductDataResponse>,
    IRequestHandler<DeleteProductDataCommand>
{
    private readonly IProductDataRepository _repository;
    private readonly IProductDataLinkRepository _productDataLinkRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IActorProvider _actorProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ResiliencePipelineProvider<string> _resiliencePipelineProvider;
    private readonly ILogger<ProductDataRequestHandlers> _logger;

    public ProductDataRequestHandlers(
        IProductDataRepository repository,
        IProductDataLinkRepository productDataLinkRepository,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ResiliencePipelineProvider<string> resiliencePipelineProvider,
        ILogger<ProductDataRequestHandlers> logger)
    {
        _repository = repository;
        _productDataLinkRepository = productDataLinkRepository;
        _tenantProvider = tenantProvider;
        _actorProvider = actorProvider;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _resiliencePipelineProvider = resiliencePipelineProvider;
        _logger = logger;
    }

    public async Task<ProductDataResponse?> Handle(GetProductDataByIdQuery request, CancellationToken ct)
    {
        var tenantId = _tenantProvider.TenantId;
        var data = await _repository.GetByIdAsync(request.Id, ct);

        if (data is null || data.TenantId != tenantId)
            return null;

        return data.ToResponse();
    }

    public async Task<List<ProductDataResponse>> Handle(GetProductDataQuery request, CancellationToken ct)
    {
        var items = await _repository.GetAllAsync(request.Type, ct);
        return items.Select(item => item.ToResponse()).ToList();
    }

    public async Task<ProductDataResponse> Handle(CreateImageProductDataCommand command, CancellationToken ct)
    {
        var entity = new ImageProductData
        {
            TenantId = _tenantProvider.TenantId,
            Title = command.Request.Title,
            Description = command.Request.Description,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            Width = command.Request.Width,
            Height = command.Request.Height,
            Format = command.Request.Format,
            FileSizeBytes = command.Request.FileSizeBytes
        };

        var created = await _repository.CreateAsync(entity, ct);
        return created.ToResponse();
    }

    public async Task<ProductDataResponse> Handle(CreateVideoProductDataCommand command, CancellationToken ct)
    {
        var entity = new VideoProductData
        {
            TenantId = _tenantProvider.TenantId,
            Title = command.Request.Title,
            Description = command.Request.Description,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            DurationSeconds = command.Request.DurationSeconds,
            Resolution = command.Request.Resolution,
            Format = command.Request.Format,
            FileSizeBytes = command.Request.FileSizeBytes
        };

        var created = await _repository.CreateAsync(entity, ct);
        return created.ToResponse();
    }

    public async Task Handle(DeleteProductDataCommand command, CancellationToken ct)
    {
        var tenantId = _tenantProvider.TenantId;

        var data = await _repository.GetByIdAsync(command.Id, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.ProductData), command.Id, ErrorCatalog.ProductData.NotFound);

        if (data.TenantId != tenantId)
            throw new NotFoundException(nameof(Domain.Entities.ProductData), command.Id, ErrorCatalog.ProductData.NotFound);

        var deletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var actorId = _actorProvider.ActorId;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _productDataLinkRepository.SoftDeleteActiveLinksForProductDataAsync(command.Id, ct);
        }, ct);

        var pipeline = _resiliencePipelineProvider.GetPipeline(ResiliencePipelineKeys.MongoProductDataDelete);

        try
        {
            // PostgreSQL link soft-delete and MongoDB document soft-delete are not atomic.
            await pipeline.ExecuteAsync(
                async token => await _repository.SoftDeleteAsync(data.Id, actorId, deletedAtUtc, token),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to soft-delete ProductData document {ProductDataId} for tenant {TenantId}. Related ProductDataLinks may already be soft-deleted in PostgreSQL.",
                data.Id,
                tenantId);
            throw;
        }
    }
}
