using System.Net;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class ScalarAndOpenApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ScalarAndOpenApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApi_Endpoint_ReturnsJsonDocument()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("openapi");
        content.ShouldContain("paths");
        content.ShouldContain("ApiProblemDetails");
        content.ShouldContain("application/problem+json");
    }

    [Fact]
    public async Task OpenApi_ContainsGlobalErrorResponsesForRestEndpoints()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var paths = doc.RootElement.GetProperty("paths");
        var productReviewsPath = paths.EnumerateObject()
            .FirstOrDefault(p => p.Name.Contains("productreviews", StringComparison.OrdinalIgnoreCase))
            .Value;

        productReviewsPath.ValueKind.ShouldBe(JsonValueKind.Object);

        var productReviewsPost = productReviewsPath.GetProperty("post");
        var responses = productReviewsPost.GetProperty("responses");

        responses.TryGetProperty("400", out _).ShouldBeTrue();
        responses.TryGetProperty("404", out _).ShouldBeTrue();
        responses.TryGetProperty("500", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Scalar_Endpoint_ReturnsHtml()
    {
        var response = await _client.GetAsync("/scalar/v1");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("scalar");
    }

    [Fact]
    public async Task GraphQL_Endpoint_IsAccessible()
    {
        var response = await _client.GetAsync("/graphql");

        // GraphQL endpoint returns 200 with Banana Cake Pop IDE on GET
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
