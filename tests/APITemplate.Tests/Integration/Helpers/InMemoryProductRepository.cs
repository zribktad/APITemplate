using APITemplate.Application.Features.Product.Specifications;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Repositories;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Tests.Integration.Helpers;

internal sealed class InMemoryProductRepository : ProductRepository
{
    private static readonly IReadOnlyList<ProductPriceFacetBucketResponse> DefaultPriceBuckets =
    [
        new("0 - 50", 0m, 50m, 0),
        new("50 - 100", 50m, 100m, 0),
        new("100 - 250", 100m, 250m, 0),
        new("250 - 500", 250m, 500m, 0),
        new("500+", 500m, null, 0)
    ];

    public InMemoryProductRepository(AppDbContext dbContext)
        : base(dbContext)
    {
    }

    public override Task<IReadOnlyList<ProductCategoryFacetValue>> GetCategoryFacetsAsync(ProductFilter filter, CancellationToken ct = default)
        => WithDbContextAsync(async dbContext =>
        {
            var specification = new ProductCategoryFacetSpecification(filter);
            var query = Ardalis.Specification.EntityFrameworkCore.SpecificationEvaluator.Default.GetQuery(dbContext.Products.AsQueryable(), specification);
            var products = await query.ToListAsync(ct);
            var categoryNames = await dbContext.Categories
                .AsNoTracking()
                .ToDictionaryAsync(category => category.Id, category => category.Name, ct);

            return (IReadOnlyList<ProductCategoryFacetValue>)products
                .GroupBy(product => new
                {
                    product.CategoryId,
                    CategoryName = product.CategoryId.HasValue && categoryNames.TryGetValue(product.CategoryId.Value, out var categoryName)
                        ? categoryName
                        : "Uncategorized"
                })
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key.CategoryName)
                .Select(group => new ProductCategoryFacetValue(
                    group.Key.CategoryId,
                    group.Key.CategoryName,
                    group.Count()))
                .ToArray();
        });

    public override Task<IReadOnlyList<ProductPriceFacetBucketResponse>> GetPriceFacetsAsync(ProductFilter filter, CancellationToken ct = default)
        => WithDbContextAsync(async dbContext =>
        {
            var specification = new ProductPriceFacetSpecification(filter);
            var query = Ardalis.Specification.EntityFrameworkCore.SpecificationEvaluator.Default.GetQuery(dbContext.Products.AsQueryable(), specification);
            var products = await query.ToListAsync(ct);

            return (IReadOnlyList<ProductPriceFacetBucketResponse>)DefaultPriceBuckets
                .Select(bucket => bucket with
                {
                    Count = products.Count(product =>
                        product.Price >= bucket.MinPrice &&
                        (bucket.MaxPrice is null || product.Price < bucket.MaxPrice.Value))
                })
                .ToArray();
        });
}
