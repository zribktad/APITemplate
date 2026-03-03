
namespace APITemplate.Application.Features.ProductData.Interfaces;
public interface IProductDataService
{
    Task<ProductDataResponse?> GetByIdAsync(string id, CancellationToken ct = default);

    Task<List<ProductDataResponse>> GetAllAsync(string? type = null, CancellationToken ct = default);

    Task<ProductDataResponse> CreateImageAsync(CreateImageProductDataRequest request, CancellationToken ct = default);

    Task<ProductDataResponse> CreateVideoAsync(CreateVideoProductDataRequest request, CancellationToken ct = default);

    Task DeleteAsync(string id, CancellationToken ct = default);
}
