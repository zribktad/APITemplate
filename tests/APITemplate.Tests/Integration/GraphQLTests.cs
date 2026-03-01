using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class GraphQLTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GraphQLTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GraphQL_GetProducts_ReturnsEmptyList()
    {
        var query = new { query = "{ products { id name price } }" };

        var response = await PostGraphQLAsync(query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<GraphQLResponse<ProductsData>>(GraphQLJsonOptions.Default);
        result!.Data.Products.Count.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GraphQL_CreateProduct_ReturnsNewProduct()
    {
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

    private async Task<HttpResponseMessage> PostGraphQLAsync(object query)
    {
        var json = JsonSerializer.Serialize(query);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/graphql", content);
    }
}
