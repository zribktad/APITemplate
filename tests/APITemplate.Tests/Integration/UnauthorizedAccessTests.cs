using System.Net;
using System.Text;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class UnauthorizedAccessTests
{
    private readonly HttpClient _client;

    public UnauthorizedAccessTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/v1/products")]
    [InlineData("/api/v1/categories")]
    [InlineData("/api/v1/productreviews")]
    [InlineData("/api/v1/product-data")]
    [InlineData("/api/v1/categories/00000000-0000-0000-0000-000000000001/stats")]
    public async Task GetEndpoint_WithoutToken_ReturnsUnauthorized(string endpoint)
    {
        var response = await _client.GetAsync(endpoint);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GraphQL_Mutation_WithoutToken_ReturnsUnauthorized()
    {
        var mutation = """
            {
              "query": "mutation($input: CreateProductRequestInput!) { createProduct(input: $input) { id name } }",
              "variables": {
                "input": {
                  "name": "unauthorized-mutation",
                  "price": 1.23
                }
              }
            }
            """;

        using var content = new StringContent(mutation, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/graphql", content);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
