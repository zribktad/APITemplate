using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { Username = "wrong", Password = "wrong" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Invalid username or password.");
    }
}
