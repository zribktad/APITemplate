using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class GraphQLTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private Guid _userId;

    public GraphQLTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GraphQL_GetProducts_ReturnsEmptyList()
    {
        await AuthenticateAsync();

        var query = new { query = "{ products { items { id name price } totalCount pageNumber pageSize } }" };

        var response = await PostGraphQLAsync(query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<ProductsData>>(GraphQLJsonOptions.Default);
        result!.Data.Products.Items.Count.ShouldBeGreaterThanOrEqualTo(0);
        result.Data.Products.PageNumber.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GraphQL_CreateProduct_ReturnsNewProduct()
    {
        await AuthenticateAsync();

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

        var response = await PostGraphQLAsync(query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<CreateProductData>>(GraphQLJsonOptions.Default);
        result!.Data.CreateProduct.Name.ShouldBe("GraphQL Product");
        result.Data.CreateProduct.Price.ShouldBe(49.99m);
    }

    [Fact]
    public async Task GraphQL_GetProductById_WhenExists_ReturnsProduct()
    {
        await AuthenticateAsync();

        var createQuery = new
        {
            query = @"
                mutation($input: CreateProductRequestInput!) {
                    createProduct(input: $input) { id name }
                }",
            variables = new
            {
                input = new { name = "Findable Product", price = 10.0 }
            }
        };

        var createResponse = await PostGraphQLAsync(createQuery);
        var createResult = await createResponse.Content.ReadFromJsonAsync<GraphQLResponse<CreateProductData>>(GraphQLJsonOptions.Default);
        var productId = createResult!.Data.CreateProduct.Id;

        var getQuery = new
        {
            query = $@"{{ productById(id: ""{productId}"") {{ id name }} }}"
        };

        var getResponse = await PostGraphQLAsync(getQuery);

        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getResult = await getResponse.Content.ReadFromJsonAsync<GraphQLResponse<ProductByIdData>>(GraphQLJsonOptions.Default);
        getResult!.Data.ProductById!.Name.ShouldBe("Findable Product");
    }

    [Fact]
    public async Task GraphQL_DeleteProduct_ReturnsTrue()
    {
        await AuthenticateAsync();

        var createQuery = new
        {
            query = @"
                mutation($input: CreateProductRequestInput!) {
                    createProduct(input: $input) { id }
                }",
            variables = new
            {
                input = new { name = "To Delete", price = 5.0 }
            }
        };

        var createResponse = await PostGraphQLAsync(createQuery);
        var createResult = await createResponse.Content.ReadFromJsonAsync<GraphQLResponse<CreateProductData>>(GraphQLJsonOptions.Default);
        var productId = createResult!.Data.CreateProduct.Id;

        var deleteQuery = new
        {
            query = $@"mutation {{ deleteProduct(id: ""{productId}"") }}"
        };

        var deleteResponse = await PostGraphQLAsync(deleteQuery);

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<GraphQLResponse<DeleteProductData>>(GraphQLJsonOptions.Default);
        deleteResult!.Data.DeleteProduct.ShouldBeTrue();
    }

    [Fact]
    public async Task GraphQL_GetProducts_WithFilterSortAndPaging_ReturnsExpectedOrderAndSlice()
    {
        await AuthenticateAsync();

        var prefix = $"sort-{Guid.NewGuid():N}";
        await CreateProductViaGraphQLAsync($"{prefix}-A", 30m);
        await CreateProductViaGraphQLAsync($"{prefix}-B", 10m);
        await CreateProductViaGraphQLAsync($"{prefix}-C", 20m);

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

        var response = await PostGraphQLAsync(query);
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
        await AuthenticateAsync();

        var prefix = $"dl-{Guid.NewGuid():N}";
        var p1 = await CreateProductViaGraphQLAsync($"{prefix}-P1", 11m);
        var p2 = await CreateProductViaGraphQLAsync($"{prefix}-P2", 22m);

        await CreateReviewViaGraphQLAsync(p1, 5);
        await CreateReviewViaGraphQLAsync(p1, 4);
        await CreateReviewViaGraphQLAsync(p2, 3);

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

        var response = await PostGraphQLAsync(query);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<ProductsWithReviewsData>>(GraphQLJsonOptions.Default);
        var items = result!.Data.Products.Items;

        items.Count.ShouldBeGreaterThanOrEqualTo(2);
        items.ShouldContain(x => x.Id == p1 && x.Reviews.Count >= 2);
        items.ShouldContain(x => x.Id == p2 && x.Reviews.Count >= 1);
    }

    private async Task AuthenticateAsync()
    {
        _userId = await IntegrationAuthHelper.AuthenticateAndGetUserIdAsync(_client, _factory.Services);
    }

    private async Task<HttpResponseMessage> PostGraphQLAsync(object query)
    {
        var json = JsonSerializer.Serialize(query);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/graphql", content);
    }

    private async Task<Guid> CreateProductViaGraphQLAsync(string name, decimal price)
    {
        var mutation = new
        {
            query = @"
                mutation($input: CreateProductRequestInput!) {
                    createProduct(input: $input) { id }
                }",
            variables = new
            {
                input = new { name, price }
            }
        };

        var response = await PostGraphQLAsync(mutation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<CreateProductData>>(GraphQLJsonOptions.Default);
        return result!.Data.CreateProduct.Id;
    }

    private async Task<Guid> CreateReviewViaGraphQLAsync(Guid productId, int rating)
    {
        var mutation = new
        {
            query = @"
                mutation($input: CreateProductReviewRequestInput!) {
                    createProductReview(input: $input) { id }
                }",
            variables = new
            {
                input = new
                {
                    productId,
                    rating
                }
            }
        };

        var response = await PostGraphQLAsync(mutation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<CreateProductReviewData>>(GraphQLJsonOptions.Default);
        return result!.Data.CreateProductReview.Id;
    }
}
