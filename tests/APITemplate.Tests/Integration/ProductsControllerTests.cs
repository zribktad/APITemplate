using System.Net;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class ProductsControllerTests
{
    private readonly HttpClient _client;

    public ProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_WithValidToken_ReturnsOk()
    {
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.GetAsync("/api/v1/products");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
