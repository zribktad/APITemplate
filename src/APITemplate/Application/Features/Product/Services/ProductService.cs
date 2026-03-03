using APITemplate.Application.Features.Product.Mappings;
using ProductEntity = APITemplate.Domain.Entities.Product;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.Product.Services;
public sealed class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly IProductQueryService _queryService;
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(
        IProductRepository repository,
        IProductQueryService queryService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _queryService = queryService;
        _unitOfWork = unitOfWork;
    }

    public Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _queryService.GetByIdAsync(id, ct);

    public Task<PagedResponse<ProductResponse>> GetAllAsync(ProductFilter filter, CancellationToken ct = default)
        => _queryService.GetPagedAsync(filter, ct);

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var product = new ProductEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            CategoryId = request.CategoryId,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(product, ct);
        await _unitOfWork.CommitAsync(ct);
        return product.ToResponse();
    }

    public async Task UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(
                nameof(ProductEntity),
                id,
                ErrorCatalog.Products.NotFound);

        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.CategoryId = request.CategoryId;

        await _repository.UpdateAsync(product, ct);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
        await _unitOfWork.CommitAsync(ct);
    }

}
