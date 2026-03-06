using System.Net;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class ProductsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/products");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_WithValidToken_ReturnsOk()
    {
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.GetAsync("/api/v1/products");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
