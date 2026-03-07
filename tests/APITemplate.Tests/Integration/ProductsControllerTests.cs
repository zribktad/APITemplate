using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class ProductsControllerTests
{
    private readonly HttpClient _client;
    private readonly Mock<IProductDataRepository> _productDataRepositoryMock;

    public ProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _productDataRepositoryMock = factory.Services.GetRequiredService<Mock<IProductDataRepository>>();
        _productDataRepositoryMock.Reset();
    }

    [Fact]
    public async Task GetAll_WithValidToken_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.GetAsync("/api/v1/products", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateAndGetById_WithProductDataIds_RoundTripsIds()
    {
        var ct = TestContext.Current.CancellationToken;
        var productDataId = Guid.NewGuid();
        IntegrationAuthHelper.Authenticate(_client);

        _productDataRepositoryMock
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ImageProductData { Id = productDataId, Title = "Image" }]);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                name = "Product with data",
                description = "Test product",
                price = 25,
                productDataIds = new[] { productDataId, productDataId }
            },
            ct);

        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, createBody);
        var created = JsonSerializer.Deserialize<JsonElement>(createBody);
        created.GetProperty("productDataIds").EnumerateArray().Select(x => x.GetGuid()).ShouldBe([productDataId]);

        var productId = created.GetProperty("id").GetGuid();

        var getResponse = await _client.GetAsync($"/api/v1/products/{productId}", ct);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        fetched.GetProperty("productDataIds").EnumerateArray().Select(x => x.GetGuid()).ShouldBe([productDataId]);
    }

    [Fact]
    public async Task Create_WithInvalidProductDataId_ReturnsBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                name = "Invalid product data",
                price = 25,
                productDataIds = new[] { "bad-id" }
            },
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
    }
}
