namespace APITemplate.Tests.Integration;

public sealed record ProductReviewItem(Guid Id, Guid UserId, int Rating, Guid ProductId);

public sealed record ProductReviewPage(List<ProductReviewItem> Items, int TotalCount, int PageNumber, int PageSize);

public sealed record CreateProductReviewData(ProductReviewItem CreateProductReview);

public sealed record ReviewsData(ProductReviewPage Reviews);

public sealed record ReviewsByProductIdData(ProductReviewPage ReviewsByProductId);

public sealed record DeleteProductReviewData(bool DeleteProductReview);
