using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class CategoriesControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CategoriesControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAll_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/categories");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FullCrudFlow_WorksWithAuthentication()
    {
        IntegrationAuthHelper.Authenticate(_client);

        // 1. Get all - empty
        var getAllResponse = await _client.GetAsync("/api/v1/categories");
        getAllResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var allCategories = await getAllResponse.Content.ReadFromJsonAsync<JsonElement[]>(TestJsonOptions.CaseInsensitive);
        allCategories.ShouldNotBeNull();

        // 2. Create category
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Name = "Electronics", Description = "Electronic devices" });

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var categoryId = created.GetProperty("id").GetString();
        categoryId.ShouldNotBeNullOrWhiteSpace();
        created.GetProperty("name").GetString().ShouldBe("Electronics");
        created.GetProperty("description").GetString().ShouldBe("Electronic devices");

        // 3. Get by id
        var getByIdResponse = await _client.GetAsync($"/api/v1/categories/{categoryId}");
        getByIdResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var fetched = await getByIdResponse.Content.ReadFromJsonAsync<JsonElement>();
        fetched.GetProperty("name").GetString().ShouldBe("Electronics");

        // 4. Update category
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1/categories/{categoryId}",
            new { Name = "Updated Electronics", Description = "Updated description" });

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 5. Verify update
        var verifyResponse = await _client.GetAsync($"/api/v1/categories/{categoryId}");
        var updated = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>();
        updated.GetProperty("name").GetString().ShouldBe("Updated Electronics");
        updated.GetProperty("description").GetString().ShouldBe("Updated description");

        // 6. Delete category
        var deleteResponse = await _client.DeleteAsync($"/api/v1/categories/{categoryId}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 7. Verify deletion
        var getDeletedResponse = await _client.GetAsync($"/api/v1/categories/{categoryId}");
        getDeletedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_NonExistentCategory_ReturnsNotFound()
    {
        IntegrationAuthHelper.Authenticate(_client);

        var response = await _client.GetAsync($"/api/v1/categories/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CategoryWithoutDescription_Succeeds()
    {
        IntegrationAuthHelper.Authenticate(_client);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Name = "Books" });

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("name").GetString().ShouldBe("Books");

        var descriptionElement = created.GetProperty("description");
        descriptionElement.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Create_MultipleCategories_AllReturnedInGetAll()
    {
        IntegrationAuthHelper.Authenticate(_client);

        await _client.PostAsJsonAsync("/api/v1/categories", new { Name = "Category A" });
        await _client.PostAsJsonAsync("/api/v1/categories", new { Name = "Category B" });

        var response = await _client.GetAsync("/api/v1/categories");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var categories = await response.Content.ReadFromJsonAsync<JsonElement[]>(TestJsonOptions.CaseInsensitive);
        categories.ShouldNotBeNull();
        categories!.Length.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetStats_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync($"/api/v1/categories/{Guid.NewGuid()}/stats");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}

