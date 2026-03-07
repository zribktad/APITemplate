namespace APITemplate.Tests.Integration;

public sealed record ProductItem(Guid Id, string Name, decimal Price, List<Guid>? ProductDataIds = null);

public sealed record ProductPage(List<ProductItem> Items, int TotalCount, int PageNumber, int PageSize);

public sealed record ProductsData(ProductPage Products);

public sealed record ProductReviewNestedItem(Guid Id, int Rating, Guid ProductId);

public sealed record ProductWithReviewsItem(Guid Id, string Name, decimal Price, List<ProductReviewNestedItem> Reviews);

public sealed record ProductWithReviewsPage(List<ProductWithReviewsItem> Items, int TotalCount, int PageNumber, int PageSize);

public sealed record ProductsWithReviewsData(ProductWithReviewsPage Products);

public sealed record CreateProductData(ProductItem CreateProduct);

public sealed record ProductByIdData(ProductItem? ProductById);

public sealed record DeleteProductData(bool DeleteProduct);
