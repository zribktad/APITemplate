using APITemplate.Application.Features.Product.Interfaces;
using APITemplate.Application.Features.Product.Specifications;
using APITemplate.Infrastructure.Persistence;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Repositories;

public sealed class ProductReadRepository : IProductReadRepository
{
    private static readonly IReadOnlyList<ProductPriceFacetBucketResponse> DefaultPriceBuckets =
    [
        new("0 - 50", 0m, 50m, 0),
        new("50 - 100", 50m, 100m, 0),
        new("100 - 250", 100m, 250m, 0),
        new("250 - 500", 250m, 500m, 0),
        new("500+", 500m, null, 0)
    ];

    private readonly IServiceScopeFactory _scopeFactory;

    public ProductReadRepository(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task<IReadOnlyList<ProductResponse>> ListAsync(ProductFilter filter, CancellationToken ct = default)
        => WithDbContextAsync(async dbContext =>
        {
            var specification = new ProductSpecification(filter);
            var query = SpecificationEvaluator.Default.GetQuery(dbContext.Products.AsQueryable(), specification);
            return (IReadOnlyList<ProductResponse>)await query.ToListAsync(ct);
        });

    public Task<int> CountAsync(ProductFilter filter, CancellationToken ct = default)
        => WithDbContextAsync(async dbContext =>
        {
            var specification = new ProductCountSpecification(filter);
            var query = SpecificationEvaluator.Default.GetQuery(dbContext.Products.AsQueryable(), specification);
            return await query.CountAsync(ct);
        });

    public Task<IReadOnlyList<ProductCategoryFacetValue>> GetCategoryFacetsAsync(ProductFilter filter, CancellationToken ct = default)
        => WithDbContextAsync(async dbContext =>
        {
            var specification = new ProductCategoryFacetSpecification(filter);
            var query = SpecificationEvaluator.Default.GetQuery(dbContext.Products.AsQueryable(), specification);

            return (IReadOnlyList<ProductCategoryFacetValue>)await query
                .GroupBy(product => new
                {
                    product.CategoryId,
                    CategoryName = product.Category != null ? product.Category.Name : "Uncategorized"
                })
                .Select(group => new
                {
                    group.Key.CategoryId,
                    group.Key.CategoryName,
                    Count = group.Count()
                })
                .OrderByDescending(group => group.Count)
                .ThenBy(group => group.CategoryName)
                .Select(group => new ProductCategoryFacetValue(
                    group.CategoryId,
                    group.CategoryName,
                    group.Count))
                .ToArrayAsync(ct);
        });

    public Task<IReadOnlyList<ProductPriceFacetBucketResponse>> GetPriceFacetsAsync(ProductFilter filter, CancellationToken ct = default)
        => WithDbContextAsync(async dbContext =>
        {
            var specification = new ProductPriceFacetSpecification(filter);
            var query = SpecificationEvaluator.Default.GetQuery(dbContext.Products.AsQueryable(), specification);

            var counts = await query
                .GroupBy(_ => 1)
                .Select(group => new PriceFacetCounts(
                    group.Count(product => product.Price >= 0m && product.Price < 50m),
                    group.Count(product => product.Price >= 50m && product.Price < 100m),
                    group.Count(product => product.Price >= 100m && product.Price < 250m),
                    group.Count(product => product.Price >= 250m && product.Price < 500m),
                    group.Count(product => product.Price >= 500m)))
                .SingleOrDefaultAsync(ct);

            return (IReadOnlyList<ProductPriceFacetBucketResponse>)DefaultPriceBuckets
                .Select(bucket => bucket with
                {
                    Count = bucket.Label switch
                    {
                        "0 - 50" => counts?.ZeroToFifty ?? 0,
                        "50 - 100" => counts?.FiftyToOneHundred ?? 0,
                        "100 - 250" => counts?.OneHundredToTwoHundredFifty ?? 0,
                        "250 - 500" => counts?.TwoHundredFiftyToFiveHundred ?? 0,
                        "500+" => counts?.FiveHundredAndAbove ?? 0,
                        _ => 0
                    }
                })
                .ToArray();
        });

    private async Task<TResult> WithDbContextAsync<TResult>(
        Func<AppDbContext, Task<TResult>> action)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(dbContext);
    }

    private sealed record PriceFacetCounts(
        int ZeroToFifty,
        int FiftyToOneHundred,
        int OneHundredToTwoHundredFifty,
        int TwoHundredFiftyToFiveHundred,
        int FiveHundredAndAbove);
}
