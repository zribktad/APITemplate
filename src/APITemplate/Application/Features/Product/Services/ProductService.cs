using APITemplate.Domain.Entities;
using APITemplate.Application.Features.Product.Mappings;
using APITemplate.Application.Features.Product.Specifications;
using ProductEntity = APITemplate.Domain.Entities.Product;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.Product.Services;
public sealed class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly IProductQueryService _queryService;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IProductDataRepository _productDataRepository;
    private readonly IProductDataLinkRepository _productDataLinkRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(
        IProductRepository repository,
        IProductQueryService queryService,
        ICategoryRepository categoryRepository,
        IProductDataRepository productDataRepository,
        IProductDataLinkRepository productDataLinkRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _queryService = queryService;
        _categoryRepository = categoryRepository;
        _productDataRepository = productDataRepository;
        _productDataLinkRepository = productDataLinkRepository;
        _unitOfWork = unitOfWork;
    }

    public Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _queryService.GetByIdAsync(id, ct);

    public Task<PagedResponse<ProductResponse>> GetAllAsync(ProductFilter filter, CancellationToken ct = default)
        => _queryService.GetPagedAsync(filter, ct);

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        await ValidateCategoryExistsAsync(request.CategoryId, ct);
        var productDataIds = await ValidateAndNormalizeProductDataIdsAsync(request.ProductDataIds ?? [], ct);

        var product = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var productId = Guid.NewGuid();
            var entity = new ProductEntity
            {
                Id = productId,
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                CategoryId = request.CategoryId,
                ProductDataLinks = productDataIds
                    .Select(productDataId => ProductDataLink.Create(productId, productDataId, Guid.Empty))
                    .ToList()
            };

            await _repository.AddAsync(entity, ct);
            return entity;
        }, ct);

        return product.ToResponse();
    }

    public async Task UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _repository.FirstOrDefaultAsync(new ProductByIdWithLinksSpecification(id), ct)
            ?? throw new NotFoundException(
                nameof(ProductEntity),
                id,
                ErrorCatalog.Products.NotFound);

        await ValidateCategoryExistsAsync(request.CategoryId, ct);
        var productDataIds = await ValidateAndNormalizeProductDataIdsAsync(request.ProductDataIds ?? [], ct);
        var allLinks = await _productDataLinkRepository.ListByProductIdAsync(id, includeDeleted: true, ct);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            product.UpdateDetails(request.Name, request.Description, request.Price, request.CategoryId);
            product.SyncProductDataLinks(productDataIds, allLinks);

            await _repository.UpdateAsync(product, ct);
        }, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _repository.FirstOrDefaultAsync(new ProductByIdWithLinksSpecification(id), ct)
            ?? throw new NotFoundException(
                nameof(ProductEntity),
                id,
                ErrorCatalog.Products.NotFound);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            product.SoftDeleteProductDataLinks();
            await _repository.DeleteAsync(product, ct);
        }, ct);
    }

    private async Task ValidateCategoryExistsAsync(Guid? categoryId, CancellationToken ct)
    {
        if (!categoryId.HasValue)
            return;

        _ = await _categoryRepository.GetByIdAsync(categoryId.Value, ct)
            ?? throw new NotFoundException(
                nameof(Domain.Entities.Category),
                categoryId.Value,
                ErrorCatalog.Categories.NotFound);
    }

    private async Task<IReadOnlyCollection<Guid>> ValidateAndNormalizeProductDataIdsAsync(
        IReadOnlyCollection<Guid> productDataIds,
        CancellationToken ct)
    {
        var normalizedIds = productDataIds
            .Distinct()
            .ToArray();

        if (normalizedIds.Length == 0)
            return normalizedIds;

        var existingIds = (await _productDataRepository.GetByIdsAsync(normalizedIds, ct))
            .Select(productData => productData.Id)
            .ToHashSet();

        var missingIds = normalizedIds
            .Where(id => !existingIds.Contains(id))
            .ToArray();

        if (missingIds.Length > 0)
        {
            throw new NotFoundException(
                nameof(ProductData),
                string.Join(", ", missingIds),
                ErrorCatalog.Products.ProductDataNotFound);
        }

        return normalizedIds;
    }
}
