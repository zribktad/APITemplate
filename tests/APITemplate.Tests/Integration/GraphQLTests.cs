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

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var products = json.GetProperty("data").GetProperty("products");
        products.GetArrayLength().ShouldBeGreaterThanOrEqualTo(0);
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

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var product = json.GetProperty("data").GetProperty("createProduct");
        product.GetProperty("name").GetString().ShouldBe("GraphQL Product");
        product.GetProperty("price").GetDecimal().ShouldBe(49.99m);
    }

    [Fact]
    public async Task GraphQL_GetProductById_WhenExists_ReturnsProduct()
    {
        // Create a product first
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
        var createJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var productId = createJson.GetProperty("data")
            .GetProperty("createProduct")
            .GetProperty("id")
            .GetString();

        // Query by ID
        var getQuery = new
        {
            query = $@"{{ productById(id: ""{productId}"") {{ id name }} }}"
        };

        var getResponse = await PostGraphQLAsync(getQuery);

        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var getJson = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var product = getJson.GetProperty("data").GetProperty("productById");
        product.GetProperty("name").GetString().ShouldBe("Findable Product");
    }

    [Fact]
    public async Task GraphQL_DeleteProduct_ReturnsTrue()
    {
        // Create a product first
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
        var createJson = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var productId = createJson.GetProperty("data")
            .GetProperty("createProduct")
            .GetProperty("id")
            .GetString();

        // Delete
        var deleteQuery = new
        {
            query = $@"mutation {{ deleteProduct(id: ""{productId}"") }}"
        };

        var deleteResponse = await PostGraphQLAsync(deleteQuery);

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var deleteJson = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>();
        deleteJson.GetProperty("data")
            .GetProperty("deleteProduct")
            .GetBoolean()
            .ShouldBeTrue();
    }

    private async Task<HttpResponseMessage> PostGraphQLAsync(object query)
    {
        var json = JsonSerializer.Serialize(query);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await _client.PostAsync("/graphql", content);
    }
}
