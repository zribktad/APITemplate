namespace APITemplate.Tests.Integration;

public sealed record ProductReviewItem(Guid Id, string ReviewerName, int Rating, Guid ProductId);

public sealed record ProductReviewConnection(List<ProductReviewItem> Nodes);

public sealed record CreateProductReviewData(ProductReviewItem CreateProductReview);

public sealed record ReviewsData(ProductReviewConnection Reviews);

public sealed record ReviewsByProductIdData(ProductReviewConnection ReviewsByProductId);

public sealed record DeleteProductReviewData(bool DeleteProductReview);
