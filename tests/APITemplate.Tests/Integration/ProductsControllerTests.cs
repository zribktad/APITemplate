using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

[Collection("Integration.ProductDataController")]
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
        var payload = await response.Content.ReadFromJsonAsync<ProductsResponse>(TestJsonOptions.CaseInsensitive, ct);
        payload.ShouldNotBeNull();
        payload!.Page.Items.ShouldNotBeNull();
        payload.Facets.ShouldNotBeNull();
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
        var created = JsonSerializer.Deserialize<ProductResponse>(createBody, TestJsonOptions.CaseInsensitive);
        created.ShouldNotBeNull();
        created!.ProductDataIds.ShouldBe([productDataId]);

        var getResponse = await _client.GetAsync($"/api/v1/products/{created.Id}", ct);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(TestJsonOptions.CaseInsensitive, ct);
        fetched.ShouldNotBeNull();
        fetched!.ProductDataIds.ShouldBe([productDataId]);
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

    [Fact]
    public async Task Update_WithoutProductDataIds_PreservesExistingLinks()
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
                productDataIds = new[] { productDataId }
            },
            ct);

        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, createBody);
        var created = JsonSerializer.Deserialize<ProductResponse>(createBody, TestJsonOptions.CaseInsensitive);
        created.ShouldNotBeNull();

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1/products/{created!.Id}",
            new
            {
                name = "Renamed product",
                description = "Updated",
                price = 30
            },
            ct);

        var updateBody = await updateResponse.Content.ReadAsStringAsync(ct);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent, updateBody);

        var getResponse = await _client.GetAsync($"/api/v1/products/{created.Id}", ct);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<ProductResponse>(TestJsonOptions.CaseInsensitive, ct);
        fetched.ShouldNotBeNull();
        fetched!.ProductDataIds.ShouldBe([productDataId]);
    }

    [Fact]
    public async Task GetAll_WithCategoryFilterAndFacets_ReturnsFilteredProductsAndFacetCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var electronicsResponse = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Name = "Electronics", Description = "Devices and accessories" },
            ct);
        var booksResponse = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Name = "Books", Description = "Printed books" },
            ct);

        var electronics = await electronicsResponse.Content.ReadFromJsonAsync<CategoryResponse>(TestJsonOptions.CaseInsensitive, ct);
        var books = await booksResponse.Content.ReadFromJsonAsync<CategoryResponse>(TestJsonOptions.CaseInsensitive, ct);
        electronics.ShouldNotBeNull();
        books.ShouldNotBeNull();
        var electronicsId = electronics!.Id;
        var booksId = books!.Id;

        await _client.PostAsJsonAsync(
            "/api/v1/products",
            new { Name = "Wireless Mouse", Description = "Comfortable office mouse", Price = 30, CategoryId = electronicsId },
            ct);
        await _client.PostAsJsonAsync(
            "/api/v1/products",
            new { Name = "Wireless Keyboard", Description = "Mechanical office keyboard", Price = 80, CategoryId = electronicsId },
            ct);
        await _client.PostAsJsonAsync(
            "/api/v1/products",
            new { Name = "Fantasy Novel", Description = "Epic dragon story", Price = 15, CategoryId = booksId },
            ct);

        var response = await _client.GetAsync($"/api/v1/products?categoryIds={electronicsId}", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ProductsResponse>(TestJsonOptions.CaseInsensitive, ct);
        payload.ShouldNotBeNull();

        payload!.Page.Items.Count().ShouldBe(2);
        payload.Page.Items.Select(item => item.Name).ShouldBe(["Wireless Mouse", "Wireless Keyboard"], ignoreOrder: true);
        payload.Facets.Categories.Count.ShouldBeGreaterThanOrEqualTo(2);
        payload.Facets.Categories.First().CategoryName.ShouldBe("Electronics");
        payload.Facets.Categories.First().Count.ShouldBe(2);
        payload.Facets.PriceBuckets.Single(bucket => bucket.Label == "0 - 50").Count.ShouldBe(1);
        payload.Facets.PriceBuckets.Single(bucket => bucket.Label == "50 - 100").Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetAll_AfterCreate_ReturnsNewProduct_WhenCacheIsInvalidated()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        var initialResponse = await _client.GetAsync("/api/v1/products", ct);
        initialResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var initialPayload = await initialResponse.Content.ReadFromJsonAsync<ProductsResponse>(TestJsonOptions.CaseInsensitive, ct);
        initialPayload.ShouldNotBeNull();
        var initialCount = initialPayload!.Page.Items.Count();

        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new
            {
                name = "Cached product",
                description = "Created while cache is warm",
                price = 10
            },
            ct);

        var createBody = await createResponse.Content.ReadAsStringAsync(ct);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, createBody);

        var secondResponse = await _client.GetAsync("/api/v1/products", ct);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<ProductsResponse>(TestJsonOptions.CaseInsensitive, ct);
        secondPayload.ShouldNotBeNull();
        secondPayload!.Page.Items.Count().ShouldBe(initialCount + 1);
    }

    [Fact]
    public async Task GetAll_WhenRateLimitExceeded_ReturnsTooManyRequests()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client);

        HttpResponseMessage? lastResponse = null;

        for (var i = 0; i < 110; i++)
        {
            lastResponse = await _client.GetAsync("/api/v1/products", ct);
            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
                break;
        }

        lastResponse.ShouldNotBeNull();
        lastResponse!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
