using System.Net;
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
