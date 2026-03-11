using System.Net;
using System.Net.Http.Json;
using APITemplate.Tests.Integration.Helpers;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration.Security;

[Collection("Integration.ProductDataController")]
public class PermissionAuthorizationIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public PermissionAuthorizationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task User_CanGetProducts()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.AuthenticateAsUser(client);

        var response = await client.GetAsync("/api/v1/products", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task User_CannotCreateProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.AuthenticateAsUser(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/products",
            new { Name = "Forbidden", Description = "Should fail", Price = 1.00 },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TenantAdmin_CanCreateProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.AuthenticateAsTenantAdmin(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/products",
            new { Name = "TenantAdmin Product", Description = "Should succeed", Price = 10.00 },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task TenantAdmin_CannotCreateUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.AuthenticateAsTenantAdmin(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/users",
            new { Username = "newuser", Email = "new@example.com", Password = "P@ssw0rd123!" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlatformAdmin_CanCreateProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.Authenticate(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/products",
            new { Name = "Admin Product", Description = "Should succeed", Price = 20.00 },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PlatformAdmin_CanGetUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.Authenticate(client);

        var response = await client.GetAsync("/api/v1/users", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task User_CannotDeleteProduct()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.AuthenticateAsUser(client);

        var response = await client.DeleteAsync($"/api/v1/products/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TenantAdmin_CanReadUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.AuthenticateAsTenantAdmin(client);

        var response = await client.GetAsync("/api/v1/users", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task User_CanCreateProductReview()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        IntegrationAuthHelper.AuthenticateAsUser(client);

        // First create a product as PlatformAdmin
        var adminClient = _factory.CreateClient();
        IntegrationAuthHelper.Authenticate(adminClient);
        var productResponse = await adminClient.PostAsJsonAsync(
            "/api/v1/products",
            new { Name = "Review Target", Description = "For review", Price = 5.00 },
            ct);
        productResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductResponse>(
            TestJsonOptions.CaseInsensitive, ct);
        product.ShouldNotBeNull();

        // Then create a review as User
        var response = await client.PostAsJsonAsync(
            "/api/v1/productreviews",
            new { ProductId = product.Id, Rating = 5, Comment = "Great!" },
            ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
