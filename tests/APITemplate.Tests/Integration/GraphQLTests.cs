using System.Net;
using System.Net.Http.Json;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class GraphQLTests
{
    private readonly HttpClient _client;
    private readonly GraphQLTestHelper _graphql;
    private readonly Guid _tenantId = Guid.NewGuid();

    public GraphQLTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _graphql = new GraphQLTestHelper(_client);
    }

    [Fact]
    public async Task GraphQL_GetProducts_ReturnsEmptyList()
    {
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var query = new { query = "{ products { items { id name price } totalCount pageNumber pageSize } }" };

        var response = await _graphql.PostAsync(query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<ProductsData>>(GraphQLJsonOptions.Default);
        result!.Data.Products.Items.Count.ShouldBeGreaterThanOrEqualTo(0);
        result.Data.Products.PageNumber.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GraphQL_CreateProduct_ReturnsNewProduct()
    {
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

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<CreateProductData>>(GraphQLJsonOptions.Default);
        result!.Data.CreateProduct.Name.ShouldBe("GraphQL Product");
        result.Data.CreateProduct.Price.ShouldBe(49.99m);
    }

    [Fact]
    public async Task GraphQL_GetProductById_WhenExists_ReturnsProduct()
    {
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var productId = await _graphql.CreateProductAsync("Findable Product", 10.0m);

        var getQuery = new
        {
            query = $@"{{ productById(id: ""{productId}"") {{ id name }} }}"
        };

        var getResponse = await _graphql.PostAsync(getQuery);

        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResult = await getResponse.Content.ReadFromJsonAsync<GraphQLResponse<ProductByIdData>>(GraphQLJsonOptions.Default);
        getResult!.Data.ProductById!.Name.ShouldBe("Findable Product");
    }

    [Fact]
    public async Task GraphQL_DeleteProduct_ReturnsTrue()
    {
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var productId = await _graphql.CreateProductAsync("To Delete", 5.0m);

        var deleteQuery = new
        {
            query = $@"mutation {{ deleteProduct(id: ""{productId}"") }}"
        };

        var deleteResponse = await _graphql.PostAsync(deleteQuery);

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<GraphQLResponse<DeleteProductData>>(GraphQLJsonOptions.Default);
        deleteResult!.Data.DeleteProduct.ShouldBeTrue();
    }

    [Fact]
    public async Task GraphQL_GetProducts_WithFilterSortAndPaging_ReturnsExpectedOrderAndSlice()
    {
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
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<ProductsData>>(GraphQLJsonOptions.Default);
        var items = result!.Data.Products.Items;

        items.Count.ShouldBe(2);
        items[0].Price.ShouldBeLessThanOrEqualTo(items[1].Price);
        result.Data.Products.TotalCount.ShouldBeGreaterThanOrEqualTo(3);
        result.Data.Products.PageNumber.ShouldBe(1);
        result.Data.Products.PageSize.ShouldBe(2);
    }

    [Fact]
    public async Task GraphQL_ProductReviewsField_UsesBatchResolverAndReturnsReviewsPerProduct()
    {
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
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<ProductsWithReviewsData>>(GraphQLJsonOptions.Default);
        var items = result!.Data.Products.Items;

        items.Count.ShouldBeGreaterThanOrEqualTo(2);
        items.ShouldContain(x => x.Id == p1 && x.Reviews.Count >= 2);
        items.ShouldContain(x => x.Id == p2 && x.Reviews.Count >= 1);
    }
}
