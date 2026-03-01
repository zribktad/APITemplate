using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class AuthenticatedCrudTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthenticatedCrudTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FullCrudFlow_WorksWithAuthentication()
    {
        // 1. Login
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { Username = "admin", Password = "admin" });

        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var loginJson = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginJson.GetProperty("accessToken").GetString();
        token.ShouldNotBeNullOrWhiteSpace();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 2. Get all - empty
        var getAllResponse = await _client.GetAsync("/api/v1/products");
        getAllResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var emptyList = await getAllResponse.Content.ReadFromJsonAsync<JsonElement[]>(JsonOptions);
        emptyList.ShouldBeEmpty();

        // 3. Create product
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new { Name = "Test Product", Description = "A description", Price = 29.99 });

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var productId = created.GetProperty("id").GetString();
        productId.ShouldNotBeNullOrWhiteSpace();
        created.GetProperty("name").GetString().ShouldBe("Test Product");
        created.GetProperty("price").GetDecimal().ShouldBe(29.99m);

        // 4. Get by id
        var getByIdResponse = await _client.GetAsync($"/api/v1/products/{productId}");
        getByIdResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var fetched = await getByIdResponse.Content.ReadFromJsonAsync<JsonElement>();
        fetched.GetProperty("name").GetString().ShouldBe("Test Product");

        // 5. Update product
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1/products/{productId}",
            new { Name = "Updated Product", Description = "Updated desc", Price = 39.99 });

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 6. Verify update
        var verifyResponse = await _client.GetAsync($"/api/v1/products/{productId}");
        var updated = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("name").GetString().ShouldBe("Updated Product");
        updated.GetProperty("price").GetDecimal().ShouldBe(39.99m);

        // 7. Delete product
        var deleteResponse = await _client.DeleteAsync($"/api/v1/products/{productId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 8. Verify deletion
        var getDeletedResponse = await _client.GetAsync($"/api/v1/products/{productId}");
        getDeletedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_NonExistentProduct_ReturnsNotFound()
    {
        await AuthenticateAsync();

        var response = await _client.GetAsync($"/api/v1/products/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_MultipleProducts_AllReturnedInGetAll()
    {
        await AuthenticateAsync();

        await _client.PostAsJsonAsync("/api/v1/products",
            new { Name = "Product A", Price = 10.0 });
        await _client.PostAsJsonAsync("/api/v1/products",
            new { Name = "Product B", Price = 20.0 });

        var response = await _client.GetAsync("/api/v1/products");
        var products = await response.Content.ReadFromJsonAsync<JsonElement[]>(JsonOptions);

        products.ShouldNotBeNull();
        products!.Length.ShouldBeGreaterThanOrEqualTo(2);
    }

    private async Task AuthenticateAsync()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { Username = "admin", Password = "admin" });

        var loginJson = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginJson.GetProperty("accessToken").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
