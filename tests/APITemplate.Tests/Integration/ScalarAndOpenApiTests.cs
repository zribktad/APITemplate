using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class ScalarAndOpenApiTests
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

        responses.TryGetProperty(StatusCodes.Status400BadRequest.ToString(), out _).ShouldBeTrue();
        responses.TryGetProperty(StatusCodes.Status401Unauthorized.ToString(), out _).ShouldBeTrue();
        responses.TryGetProperty(StatusCodes.Status403Forbidden.ToString(), out _).ShouldBeTrue();
        responses.TryGetProperty(StatusCodes.Status404NotFound.ToString(), out _).ShouldBeTrue();
        responses.TryGetProperty(StatusCodes.Status500InternalServerError.ToString(), out _).ShouldBeTrue();
    }

    [Fact]
    public async Task OpenApi_ContainsOAuth2SecurityScheme()
    {
        var response = await _client.GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        var components = doc.RootElement.GetProperty("components");
        var securitySchemes = components.GetProperty("securitySchemes");
        securitySchemes.TryGetProperty("OAuth2", out var oauth2).ShouldBeTrue();
        oauth2.GetProperty("type").GetString().ShouldBe("oauth2");
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
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.PostAsJsonAsync(
            "/graphql",
            new { query = "{ __typename }" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
