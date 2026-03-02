using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using GreenDonut;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Api.GraphQL.DataLoaders;

/// <summary>
/// Batches multiple review lookups by ProductId into a single DB query,
/// preventing the N+1 problem when reviews are resolved outside of IQueryable projection context.
/// Usage: inject into a resolver and call LoadAsync(productId).
/// </summary>
public sealed class ProductReviewsByProductDataLoader : BatchDataLoader<Guid, ProductReview[]>
{
    private readonly IProductReviewRepository _repo;

    public ProductReviewsByProductDataLoader(
        IProductReviewRepository repo,
        IBatchScheduler batchScheduler,
        DataLoaderOptions options = default!)
        : base(batchScheduler, options)
    {
        _repo = repo;
    }

    protected override async Task<IReadOnlyDictionary<Guid, ProductReview[]>> LoadBatchAsync(
        IReadOnlyList<Guid> productIds,
        CancellationToken ct)
    {
        var reviews = await _repo.AsQueryable()
            .Where(r => productIds.Contains(r.ProductId))
            .ToListAsync(ct);

        var lookup = reviews.ToLookup(r => r.ProductId);

        return productIds
            .Distinct()
            .ToDictionary(id => id, id => lookup[id].ToArray());
    }
}
