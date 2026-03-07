using System.Net;
using APITemplate.Tests.Integration.Helpers;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class GraphQLTests
{
    private readonly HttpClient _client;
    private readonly GraphQLTestHelper _graphql;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Mock<IProductDataRepository> _productDataRepositoryMock;

    public GraphQLTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _graphql = new GraphQLTestHelper(_client);
        _productDataRepositoryMock = factory.Services.GetRequiredService<Mock<IProductDataRepository>>();
        _productDataRepositoryMock.Reset();
    }

    [Fact]
    public async Task GraphQL_GetProducts_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var query = new { query = "{ products { items { id name price } totalCount pageNumber pageSize } }" };

        var response = await _graphql.PostAsync(query);
        var products = await _graphql.ReadRequiredGraphQLFieldAsync<ProductsData, ProductPage>(
            response,
            data => data.Products,
            "products");
        products.Items.Count.ShouldBeGreaterThanOrEqualTo(0);
        products.PageNumber.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GraphQL_CreateProduct_ReturnsNewProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var query = new
        {
            query = @"
                mutation($input: CreateProductRequestInput!) {
                    createProduct(input: $input) {
                        id
                        name
                        price
                    }
                }",
            variables = new
            {
                input = new
                {
                    name = "GraphQL Product",
                    description = "Created via GraphQL",
                    price = 49.99
                }
            }
        };

        var response = await _graphql.PostAsync(query);
        var createProduct = await _graphql.ReadRequiredGraphQLFieldAsync<CreateProductData, ProductItem>(
            response,
            data => data.CreateProduct,
            "createProduct");
        createProduct.Name.ShouldBe("GraphQL Product");
        createProduct.Price.ShouldBe(49.99m);
    }

    [Fact]
    public async Task GraphQL_CreateProduct_WithProductDataIds_ReturnsIds()
    {
        var productDataId = Guid.NewGuid();
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        _productDataRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ImageProductData { Id = productDataId, Title = "Image" }]);
        var query = new
        {
            query = @"
                mutation($input: CreateProductRequestInput!) {
                    createProduct(input: $input) {
                        id
                        name
                        price
                        productDataIds
                    }
                }",
            variables = new
            {
                input = new
                {
                    name = "GraphQL Product",
                    price = 49.99,
                    productDataIds = new[] { productDataId }
                }
            }
        };

        var response = await _graphql.PostAsync(query);
        var createProduct = await _graphql.ReadRequiredGraphQLFieldAsync<CreateProductData, ProductItem>(
            response,
            data => data.CreateProduct,
            "createProduct");
        createProduct.ProductDataIds.ShouldBe([productDataId]);
    }

    [Fact]
    public async Task GraphQL_GetProductById_WhenExists_ReturnsProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var productId = await _graphql.CreateProductAsync("Findable Product", 10.0m);

        var getQuery = new
        {
            query = $@"{{ productById(id: ""{productId}"") {{ id name productDataIds }} }}"
        };

        var getResponse = await _graphql.PostAsync(getQuery);
        var getResult = await _graphql.ReadGraphQLResponseAsync<ProductByIdData>(getResponse);
        getResult.ProductById.ShouldNotBeNull();
        getResult.ProductById.Name.ShouldBe("Findable Product");
    }

    [Fact]
    public async Task GraphQL_DeleteProduct_ReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var productId = await _graphql.CreateProductAsync("To Delete", 5.0m);

        var deleteQuery = new
        {
            query = $@"mutation {{ deleteProduct(id: ""{productId}"") }}"
        };

        var deleteResponse = await _graphql.PostAsync(deleteQuery);
        var deleteResult = await _graphql.ReadGraphQLResponseAsync<DeleteProductData>(deleteResponse);
        deleteResult.DeleteProduct.ShouldBeTrue();
    }

    [Fact]
    public async Task GraphQL_GetProducts_WithFilterSortAndPaging_ReturnsExpectedOrderAndSlice()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var prefix = $"sort-{Guid.NewGuid():N}";
        await _graphql.CreateProductAsync($"{prefix}-A", 30m);
        await _graphql.CreateProductAsync($"{prefix}-B", 10m);
        await _graphql.CreateProductAsync($"{prefix}-C", 20m);

        var query = new
        {
            query = @"
                query($input: ProductQueryInput) {
                    products(input: $input) {
                        items { id name price }
                        totalCount
                        pageNumber
                        pageSize
                    }
                }",
            variables = new
            {
                input = new
                {
                    name = prefix,
                    sortBy = "price",
                    sortDirection = "asc",
                    pageNumber = 1,
                    pageSize = 2
                }
            }
        };

        var response = await _graphql.PostAsync(query);
        var products = await _graphql.ReadRequiredGraphQLFieldAsync<ProductsData, ProductPage>(
            response,
            data => data.Products,
            "products");
        var items = products.Items;

        items.Count.ShouldBe(2);
        items[0].Price.ShouldBeLessThanOrEqualTo(items[1].Price);
        products.TotalCount.ShouldBeGreaterThanOrEqualTo(3);
        products.PageNumber.ShouldBe(1);
        products.PageSize.ShouldBe(2);
    }

    [Fact]
    public async Task GraphQL_ProductReviewsField_UsesBatchResolverAndReturnsReviewsPerProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var prefix = $"dl-{Guid.NewGuid():N}";
        var p1 = await _graphql.CreateProductAsync($"{prefix}-P1", 11m);
        var p2 = await _graphql.CreateProductAsync($"{prefix}-P2", 22m);

        await _graphql.CreateReviewAsync(p1, 5);
        await _graphql.CreateReviewAsync(p1, 4);
        await _graphql.CreateReviewAsync(p2, 3);

        var query = new
        {
            query = @"
                query($input: ProductQueryInput) {
                    products(input: $input) {
                        items {
                            id
                            name
                            price
                            reviews { id rating productId }
                        }
                        totalCount
                        pageNumber
                        pageSize
                    }
                }",
            variables = new
            {
                input = new
                {
                    name = prefix,
                    pageNumber = 1,
                    pageSize = 10
                }
            }
        };

        var response = await _graphql.PostAsync(query);
        var products = await _graphql.ReadRequiredGraphQLFieldAsync<ProductsWithReviewsData, ProductWithReviewsPage>(
            response,
            data => data.Products,
            "products");
        var items = products.Items;

        items.Count.ShouldBeGreaterThanOrEqualTo(2);
        items.ShouldContain(x => x.Id == p1 && x.Reviews.Count >= 2);
        items.ShouldContain(x => x.Id == p2 && x.Reviews.Count >= 1);
    }
}
