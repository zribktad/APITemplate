namespace APITemplate.Tests.Integration;

public sealed record ProductReviewItem(Guid Id, string ReviewerName, int Rating, Guid ProductId);

public sealed record CreateProductReviewData(ProductReviewItem CreateProductReview);

public sealed record ReviewsData(List<ProductReviewItem> Reviews);

public sealed record ReviewsByProductIdData(List<ProductReviewItem> ReviewsByProductId);

public sealed record DeleteProductReviewData(bool DeleteProductReview);
