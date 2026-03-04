using System.Net;
using System.Net.Http.Json;
using System.Text;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class AuthEdgeCasesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public AuthEdgeCasesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WhenUserInactive_ReturnsUnauthorized()
    {
        var username = $"inactive-user-{Guid.NewGuid():N}";
        var (tenant, _) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            "secret-pass",
            userIsActive: false);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { Username = $"{tenant.Code}\\{username}", Password = "secret-pass" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WhenTenantInactive_ReturnsUnauthorized()
    {
        var username = $"inactive-tenant-user-{Guid.NewGuid():N}";
        var (tenant, _) = await IntegrationAuthHelper.SeedTenantUserAsync(
            _factory.Services,
            username,
            $"{username}@example.com",
            "secret-pass",
            userIsActive: true,
            tenantIsActive: false);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { Username = $"{tenant.Code}\\{username}", Password = "secret-pass" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GraphQL_Mutation_WithoutToken_ReturnsGraphQLErrorPayload()
    {
        var mutation = """
            {
              "query": "mutation($input: CreateProductRequestInput!) { createProduct(input: $input) { id name } }",
              "variables": {
                "input": {
                  "name": "unauthorized-mutation",
                  "price": 1.23
                }
              }
            }
            """;

        using var content = new StringContent(mutation, Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/graphql", content);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("\"errors\"");
    }

    [Fact]
    public async Task RequestContext_WhenCorrelationHeaderProvided_EchoesHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
        request.Headers.Add("X-Correlation-Id", "corr-edge-123");

        var response = await _client.SendAsync(request);

        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("corr-edge-123");
        response.Headers.GetValues("X-Trace-Id").Single().ShouldNotBeNullOrWhiteSpace();
        response.Headers.GetValues("X-Elapsed-Ms").Single().ShouldNotBeNullOrWhiteSpace();
    }
}
