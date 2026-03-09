namespace APITemplate.Api.GraphQL.DataLoaders;
public sealed class ProductReviewsByProductDataLoader : BatchDataLoader<Guid, ProductReviewResponse[]>
{
    private readonly IProductReviewService _reviewService;

    public ProductReviewsByProductDataLoader(
        IProductReviewService reviewService,
        IBatchScheduler batchScheduler,
        DataLoaderOptions options = default!)
        : base(batchScheduler, options)
    {
        _reviewService = reviewService;
    }

    protected override async Task<IReadOnlyDictionary<Guid, ProductReviewResponse[]>> LoadBatchAsync(
        IReadOnlyList<Guid> productIds,
        CancellationToken ct)
    {
        return await _reviewService.GetByProductIdsAsync(productIds, ct);
    }
}
