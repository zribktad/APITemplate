using Shouldly;
using Xunit;

namespace APITemplate.Tests.Integration;

public class AuthEdgeCasesTests
{
    private readonly HttpClient _client;

    public AuthEdgeCasesTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RequestContext_WhenCorrelationHeaderProvided_EchoesHeader()
    {
        var ct = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
        request.Headers.Add("X-Correlation-Id", "corr-edge-123");

        var response = await _client.SendAsync(request, ct);

        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("corr-edge-123");
        response.Headers.GetValues("X-Trace-Id").Single().ShouldNotBeNullOrWhiteSpace();
        response.Headers.GetValues("X-Elapsed-Ms").Single().ShouldNotBeNullOrWhiteSpace();
    }
}
