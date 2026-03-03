using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class CorsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CorsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Preflight_FromAllowedOrigin_ReturnsCorsHeaders()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/graphql");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins).ShouldBeTrue();
        allowedOrigins!.Single().ShouldBe("http://localhost:3000");

        response.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();
    }

    [Fact]
    public async Task Preflight_FromDisallowedOrigin_DoesNotReturnAllowOriginHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/graphql");
        request.Headers.Add("Origin", "http://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await _client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task Get_FromAllowedOrigin_ReturnsAllowOriginHeaderWithoutCredentials()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/graphql");
        request.Headers.Add("Origin", "http://localhost:3000");

        var response = await _client.SendAsync(request);

        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins).ShouldBeTrue();
        allowedOrigins!.Single().ShouldBe("http://localhost:3000");
        response.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();
    }
}
