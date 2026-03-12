using System.Net;
using System.Net.Http.Json;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class TenantsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Guid _tenantId = Guid.NewGuid();

    public TenantsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        IntegrationAuthHelper.Authenticate(_client, tenantId: _tenantId);
    }

    // Unique per test-class instance → no cross-test collisions even with shared DB
    private string Code(string prefix) => $"{prefix}-{_tenantId:N}"[..20];

    private async Task<TenantResponse> CreateTenantAsync(
        string code,
        string name,
        CancellationToken ct
    )
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/tenants",
            new { Code = code, Name = name },
            ct
        );
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<TenantResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        return created!;
    }

    [Fact]
    public async Task Create_ReturnsCreatedWithCorrectData()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = Code("CR");

        var created = await CreateTenantAsync(code, "Acme Corp", ct);

        created.Id.ShouldNotBe(Guid.Empty);
        created.Code.ShouldBe(code);
        created.Name.ShouldBe("Acme Corp");
        created.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetById_ExistingTenant_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateTenantAsync(Code("GB"), "Get By Id Corp", ct);

        var response = await _client.GetAsync($"/api/v1/tenants/{created.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await response.Content.ReadFromJsonAsync<TenantResponse>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        fetched.ShouldNotBeNull();
        fetched!.Code.ShouldBe(created.Code);
    }

    [Fact]
    public async Task GetAll_ContainsCreatedTenant()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateTenantAsync(Code("GA"), "Get All Corp", ct);

        var response = await _client.GetAsync("/api/v1/tenants", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenants = await response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        tenants.ShouldNotBeNull();
        tenants!.Items.ShouldContain(t => t.Id == created.Id);
    }

    [Fact]
    public async Task Delete_ExistingTenant_ReturnsNoContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateTenantAsync(Code("DEL"), "Delete Corp", ct);

        var response = await _client.DeleteAsync($"/api/v1/tenants/{created.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_ExistingTenant_ThenGetById_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await CreateTenantAsync(Code("DN"), "Delete NotFound Corp", ct);

        await _client.DeleteAsync($"/api/v1/tenants/{created.Id}", ct);
        var response = await _client.GetAsync($"/api/v1/tenants/{created.Id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var code = Code("DUP");

        await CreateTenantAsync(code, "First", ct);
        var duplicate = await _client.PostAsJsonAsync(
            "/api/v1/tenants",
            new { Code = code, Name = "Second" },
            ct
        );

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync($"/api/v1/tenants/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonExistent_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.DeleteAsync($"/api/v1/tenants/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_ReturnsPagedEnvelope()
    {
        var ct = TestContext.Current.CancellationToken;
        await CreateTenantAsync(Code("P1"), "Paged Tenant A", ct);
        await CreateTenantAsync(Code("P2"), "Paged Tenant B", ct);

        var response = await _client.GetAsync(
            "/api/v1/tenants?pageNumber=1&pageSize=1&sortBy=code&sortDirection=asc",
            ct
        );

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        payload.ShouldNotBeNull();
        payload!.Items.Count().ShouldBe(1);
        payload.PageNumber.ShouldBe(1);
        payload.PageSize.ShouldBe(1);
        payload.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Create_MultipleTenants_AllReturnedInGetAll()
    {
        var ct = TestContext.Current.CancellationToken;
        var a = await CreateTenantAsync(Code("MA"), "Tenant A", ct);
        var b = await CreateTenantAsync(Code("MB"), "Tenant B", ct);

        var response = await _client.GetAsync("/api/v1/tenants", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenants = await response.Content.ReadFromJsonAsync<PagedResponse<TenantResponse>>(
            TestJsonOptions.CaseInsensitive,
            ct
        );
        tenants.ShouldNotBeNull();
        tenants!.Items.ShouldContain(t => t.Id == a.Id);
        tenants.Items.ShouldContain(t => t.Id == b.Id);
    }
}
