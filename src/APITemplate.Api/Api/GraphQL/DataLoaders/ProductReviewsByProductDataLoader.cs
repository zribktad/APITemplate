using MediatR;

namespace APITemplate.Api.GraphQL.DataLoaders;
public sealed class ProductReviewsByProductDataLoader : BatchDataLoader<Guid, ProductReviewResponse[]>
{
    private readonly ISender _sender;

    public ProductReviewsByProductDataLoader(
        ISender sender,
        IBatchScheduler batchScheduler,
        DataLoaderOptions options = default!)
        : base(batchScheduler, options)
    {
        _sender = sender;
    }

    protected override async Task<IReadOnlyDictionary<Guid, ProductReviewResponse[]>> LoadBatchAsync(
        IReadOnlyList<Guid> productIds,
        CancellationToken ct)
    {
        return await _sender.Send(new GetProductReviewsByProductIdsQuery(productIds), ct);
    }
}
