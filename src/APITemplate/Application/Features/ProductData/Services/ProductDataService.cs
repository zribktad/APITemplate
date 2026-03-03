using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.ProductData.Services;
public sealed class ProductDataService : IProductDataService
{
    private readonly IProductDataRepository _repository;

    public ProductDataService(IProductDataRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProductDataResponse?> GetByIdAsync(string id, CancellationToken ct = default)
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
            Title = request.Title,
            Description = request.Description,
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
            Title = request.Title,
            Description = request.Description,
            DurationSeconds = request.DurationSeconds,
            Resolution = request.Resolution,
            Format = request.Format,
            FileSizeBytes = request.FileSizeBytes
        };

        var created = await _repository.CreateAsync(entity, ct);
        return created.ToResponse();
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
    }
}
