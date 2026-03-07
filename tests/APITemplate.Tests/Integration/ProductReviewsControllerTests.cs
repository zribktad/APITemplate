using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class ProductReviewsControllerTests
{
    private readonly HttpClient _client;
    private readonly Guid _tenantId = Guid.NewGuid();

    public ProductReviewsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FullReviewFlow_CreateAndQuery()
    {
        var userId = IntegrationAuthHelper.AuthenticateAndGetUserId(_client, tenantId: _tenantId);

        // 1. Create a product
        var productResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new { Name = "Reviewed Product", Price = 49.99 });

        productResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var product = await productResponse.Content.ReadFromJsonAsync<JsonElement>();
        var productId = product.GetProperty("id").GetString()!;

        // 2. Create a review for the product
        var createReviewResponse = await _client.PostAsJsonAsync(
            "/api/v1/productreviews",
            new { ProductId = productId, Comment = "Great product!", Rating = 5 });

        createReviewResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createReviewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var reviewId = created.GetProperty("id").GetString()!;
        created.GetProperty("userId").GetGuid().ShouldBe(userId);
        created.GetProperty("rating").GetInt32().ShouldBe(5);
        created.GetProperty("productId").GetString().ShouldBe(productId);

        // 3. Get review by id
        var getByIdResponse = await _client.GetAsync($"/api/v1/productreviews/{reviewId}");
        getByIdResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await getByIdResponse.Content.ReadFromJsonAsync<JsonElement>();
        fetched.GetProperty("userId").GetGuid().ShouldBe(userId);

        // 4. Get reviews by productId
        var byProductResponse = await _client.GetAsync($"/api/v1/productreviews/by-product/{productId}");
        byProductResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reviews = await byProductResponse.Content.ReadFromJsonAsync<JsonElement[]>(TestJsonOptions.CaseInsensitive);
        reviews.ShouldNotBeNull();
        reviews!.Length.ShouldBeGreaterThanOrEqualTo(1);

        // 5. Delete the review
        var deleteResponse = await _client.DeleteAsync($"/api/v1/productreviews/{reviewId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 6. Verify deletion
        var getDeletedResponse = await _client.GetAsync($"/api/v1/productreviews/{reviewId}");
        getDeletedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_NonExistentReview_ReturnsNotFound()
    {
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var response = await _client.GetAsync($"/api/v1/productreviews/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithNonExistentProduct_ReturnsNotFound()
    {
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/productreviews",
            new { ProductId = Guid.NewGuid(), Rating = 3 });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().ShouldBe((int)HttpStatusCode.NotFound);
        problem.GetProperty("title").GetString().ShouldBe("Not Found");
        var detail = problem.GetProperty("detail").GetString();
        detail.ShouldNotBeNullOrWhiteSpace();
        detail.ShouldContain("Product with id");
        detail.ShouldContain("not found");
        problem.GetProperty("errorCode").GetString().ShouldBe(ErrorCatalog.Reviews.ProductNotFoundForReview);
        problem.GetProperty("type").GetString().ShouldBe($"https://api-template.local/errors/{ErrorCatalog.Reviews.ProductNotFoundForReview}");
        problem.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetByProductId_ReturnsEmptyForProductWithNoReviews()
    {
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var productResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new { Name = "No Review Product", Price = 9.99 });

        var product = await productResponse.Content.ReadFromJsonAsync<JsonElement>();
        var productId = product.GetProperty("id").GetString()!;

        var response = await _client.GetAsync($"/api/v1/productreviews/by-product/{productId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var reviews = await response.Content.ReadFromJsonAsync<JsonElement[]>(TestJsonOptions.CaseInsensitive);
        reviews.ShouldNotBeNull();
        reviews!.ShouldBeEmpty();
    }
}
