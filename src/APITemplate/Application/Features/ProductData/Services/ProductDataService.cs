using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Resilience;
using APITemplate.Application.Features.ProductData.Mappings;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;

namespace APITemplate.Application.Features.ProductData.Services;
public sealed class ProductDataService : IProductDataService
{
    private readonly IProductDataRepository _repository;
    private readonly IProductDataLinkRepository _productDataLinkRepository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IActorProvider _actorProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ResiliencePipelineProvider<string> _resiliencePipelineProvider;
    private readonly ILogger<ProductDataService> _logger;

    public ProductDataService(
        IProductDataRepository repository,
        IProductDataLinkRepository productDataLinkRepository,
        ITenantProvider tenantProvider,
        IActorProvider actorProvider,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ResiliencePipelineProvider<string> resiliencePipelineProvider,
        ILogger<ProductDataService> logger)
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

    public async Task<ProductDataResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var data = await _repository.GetByIdAsync(id, ct);
        return data?.ToResponse();
    }

    public async Task<List<ProductDataResponse>> GetAllAsync(string? type = null, CancellationToken ct = default)
    {
        var items = await _repository.GetAllAsync(type, ct);
        return items.Select(x => x.ToResponse()).ToList();
    }

    public async Task<ProductDataResponse> CreateImageAsync(CreateImageProductDataRequest request, CancellationToken ct = default)
    {
        var entity = new ImageProductData
        {
            TenantId = _tenantProvider.TenantId,
            Title = request.Title,
            Description = request.Description,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            Width = request.Width,
            Height = request.Height,
            Format = request.Format,
            FileSizeBytes = request.FileSizeBytes
        };

        var created = await _repository.CreateAsync(entity, ct);
        return created.ToResponse();
    }

    public async Task<ProductDataResponse> CreateVideoAsync(CreateVideoProductDataRequest request, CancellationToken ct = default)
    {
        var entity = new VideoProductData
        {
            TenantId = _tenantProvider.TenantId,
            Title = request.Title,
            Description = request.Description,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
            DurationSeconds = request.DurationSeconds,
            Resolution = request.Resolution,
            Format = request.Format,
            FileSizeBytes = request.FileSizeBytes
        };

        var created = await _repository.CreateAsync(entity, ct);
        return created.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var data = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.ProductData), id, ErrorCatalog.ProductData.NotFound);

        var deletedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var actorId = _actorProvider.ActorId;
        var tenantId = _tenantProvider.TenantId;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _productDataLinkRepository.SoftDeleteActiveLinksForProductDataAsync(id, ct);
        }, ct);

        var pipeline = _resiliencePipelineProvider.GetPipeline(ResiliencePipelineKeys.MongoProductDataDelete);
        try
        {
            // TODO: This cross-store delete flow is still not fully correct.
            // PostgreSQL link soft-delete and MongoDB document soft-delete are not atomic,
            // so partial failure can still leave the system inconsistent.
            // Logging below is only a mitigation until an outbox/eventual-consistency flow is introduced.
            await pipeline.ExecuteAsync(async token =>
            {
                await _repository.SoftDeleteAsync(data.Id, actorId, deletedAtUtc, token);
            }, ct);
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
