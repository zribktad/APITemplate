namespace APITemplate.Tests.Integration;

public sealed record ProductItem(Guid Id, string Name, decimal Price);

public sealed record ProductsData(List<ProductItem> Products);

public sealed record CreateProductData(ProductItem CreateProduct);

public sealed record ProductByIdData(ProductItem? ProductById);

public sealed record DeleteProductData(bool DeleteProduct);
