
namespace APITemplate.Application.Features.ProductData.Interfaces;
public interface IProductDataService
{
    Task<ProductDataResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<List<ProductDataResponse>> GetAllAsync(string? type = null, CancellationToken ct = default);

    Task<ProductDataResponse> CreateImageAsync(CreateImageProductDataRequest request, CancellationToken ct = default);

    Task<ProductDataResponse> CreateVideoAsync(CreateVideoProductDataRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
