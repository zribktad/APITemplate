using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class AuthenticatedCrudTests
{
    private readonly HttpClient _client;
    private readonly Guid _tenantId = Guid.NewGuid();

    public AuthenticatedCrudTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FullCrudFlow_WorksWithAuthentication()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        // 2. Get all - empty
        var getAllResponse = await _client.GetAsync("/api/v1/products", ct);
        getAllResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var pagedEmpty = await getAllResponse.Content.ReadFromJsonAsync<JsonElement>(TestJsonOptions.CaseInsensitive, ct);
        var emptyList = pagedEmpty.GetProperty("items").EnumerateArray().ToArray();
        emptyList.ShouldBeEmpty();

        // 3. Create product
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/products",
            new { Name = "Test Product", Description = "A description", Price = 29.99 },
            ct);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        var productId = created.GetProperty("id").GetString();
        productId.ShouldNotBeNullOrWhiteSpace();
        created.GetProperty("name").GetString().ShouldBe("Test Product");
        created.GetProperty("price").GetDecimal().ShouldBe(29.99m);

        // 4. Get by id
        var getByIdResponse = await _client.GetAsync($"/api/v1/products/{productId}", ct);
        getByIdResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var fetched = await getByIdResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        fetched.GetProperty("name").GetString().ShouldBe("Test Product");

        // 5. Update product
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1/products/{productId}",
            new { Name = "Updated Product", Description = "Updated desc", Price = 39.99 },
            ct);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 6. Verify update
        var verifyResponse = await _client.GetAsync($"/api/v1/products/{productId}", ct);
        var updated = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        updated.GetProperty("name").GetString().ShouldBe("Updated Product");
        updated.GetProperty("price").GetDecimal().ShouldBe(39.99m);

        // 7. Delete product
        var deleteResponse = await _client.DeleteAsync($"/api/v1/products/{productId}", ct);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 8. Verify deletion
        var getDeletedResponse = await _client.GetAsync($"/api/v1/products/{productId}", ct);
        getDeletedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_NonExistentProduct_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var response = await _client.GetAsync($"/api/v1/products/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_MultipleProducts_AllReturnedInGetAll()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        await _client.PostAsJsonAsync("/api/v1/products",
            new { Name = "Product A", Price = 10.0 }, ct);
        await _client.PostAsJsonAsync("/api/v1/products",
            new { Name = "Product B", Price = 20.0 }, ct);

        var response = await _client.GetAsync("/api/v1/products", ct);
        var pagedResponse = await response.Content.ReadFromJsonAsync<JsonElement>(TestJsonOptions.CaseInsensitive, ct);
        var products = pagedResponse.GetProperty("items").EnumerateArray().ToArray();

        products.ShouldNotBeNull();
        products.Length.ShouldBeGreaterThanOrEqualTo(2);
    }
}
