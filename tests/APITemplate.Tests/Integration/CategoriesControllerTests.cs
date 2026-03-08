using System.Net;
using System.Net.Http.Json;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class CategoriesControllerTests
{
    private readonly HttpClient _client;
    private readonly Guid _tenantId = Guid.NewGuid();

    public CategoriesControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FullCrudFlow_WorksWithAuthentication()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        // 1. Get all - empty
        var getAllResponse = await _client.GetAsync("/api/v1/categories", ct);
        getAllResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var allCategories = await getAllResponse.Content.ReadFromJsonAsync<PagedResponse<CategoryResponse>>(TestJsonOptions.CaseInsensitive, ct);
        allCategories.ShouldNotBeNull();
        allCategories!.Items.ShouldBeEmpty();

        // 2. Create category
        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Name = "Electronics", Description = "Electronic devices" },
            ct);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<CategoryResponse>(TestJsonOptions.CaseInsensitive, ct);
        created.ShouldNotBeNull();
        created!.Id.ShouldNotBe(Guid.Empty);
        created.Name.ShouldBe("Electronics");
        created.Description.ShouldBe("Electronic devices");

        // 3. Get by id
        var getByIdResponse = await _client.GetAsync($"/api/v1/categories/{created.Id}", ct);
        getByIdResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var fetched = await getByIdResponse.Content.ReadFromJsonAsync<CategoryResponse>(TestJsonOptions.CaseInsensitive, ct);
        fetched.ShouldNotBeNull();
        fetched!.Name.ShouldBe("Electronics");

        // 4. Update category
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1/categories/{created.Id}",
            new { Name = "Updated Electronics", Description = "Updated description" },
            ct);

        updateResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 5. Verify update
        var verifyResponse = await _client.GetAsync($"/api/v1/categories/{created.Id}", ct);
        var updated = await verifyResponse.Content.ReadFromJsonAsync<CategoryResponse>(TestJsonOptions.CaseInsensitive, ct);
        updated.ShouldNotBeNull();
        updated!.Name.ShouldBe("Updated Electronics");
        updated.Description.ShouldBe("Updated description");

        // 6. Delete category
        var deleteResponse = await _client.DeleteAsync($"/api/v1/categories/{created.Id}", ct);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 7. Verify deletion
        var getDeletedResponse = await _client.GetAsync($"/api/v1/categories/{created.Id}", ct);
        getDeletedResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_NonExistentCategory_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var response = await _client.GetAsync($"/api/v1/categories/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_CategoryWithoutDescription_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        var createResponse = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { Name = "Books" },
            ct);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<CategoryResponse>(TestJsonOptions.CaseInsensitive, ct);
        created.ShouldNotBeNull();
        created!.Name.ShouldBe("Books");
        created.Description.ShouldBeNull();
    }

    [Fact]
    public async Task Create_MultipleCategories_AllReturnedInGetAll()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        await _client.PostAsJsonAsync("/api/v1/categories", new { Name = "Category A" }, ct);
        await _client.PostAsJsonAsync("/api/v1/categories", new { Name = "Category B" }, ct);

        var response = await _client.GetAsync("/api/v1/categories", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var categories = await response.Content.ReadFromJsonAsync<PagedResponse<CategoryResponse>>(TestJsonOptions.CaseInsensitive, ct);
        categories.ShouldNotBeNull();
        categories!.Items.Count().ShouldBeGreaterThanOrEqualTo(2);
        categories.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAll_ReturnsPagedEnvelope()
    {
        var ct = TestContext.Current.CancellationToken;
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);

        await _client.PostAsJsonAsync("/api/v1/categories", new { Name = "Office Furniture", Description = "Desk and chair" }, ct);
        await _client.PostAsJsonAsync("/api/v1/categories", new { Name = "Kitchen Tools", Description = "Pans and knives" }, ct);

        var response = await _client.GetAsync("/api/v1/categories?pageNumber=1&pageSize=1&sortBy=name&sortDirection=asc", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<CategoryResponse>>(TestJsonOptions.CaseInsensitive, ct);
        payload.ShouldNotBeNull();
        payload!.Items.Count().ShouldBe(1);
        payload.PageNumber.ShouldBe(1);
        payload.PageSize.ShouldBe(1);
        payload.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
    }
}
