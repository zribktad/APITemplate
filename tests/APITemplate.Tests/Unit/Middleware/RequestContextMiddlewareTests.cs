using APITemplate.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Middleware;

public class RequestContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenHeaderProvided_EchoesCorrelationIdToResponse()
    {
        var logger = new Mock<ILogger<RequestContextMiddleware>>();
        var middleware = new RequestContextMiddleware(
            async ctx => await ctx.Response.WriteAsync("ok"),
            logger.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Headers[RequestContextMiddleware.CorrelationIdHeader] = "corr-123";

        await middleware.InvokeAsync(context);

        context.Response.Headers[RequestContextMiddleware.CorrelationIdHeader].ToString().ShouldBe("corr-123");
        context.Response.Headers["X-Trace-Id"].ToString().ShouldNotBeNullOrWhiteSpace();
        context.Response.Headers["X-Elapsed-Ms"].ToString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderMissing_UsesTraceIdentifierAsCorrelationId()
    {
        var logger = new Mock<ILogger<RequestContextMiddleware>>();
        var middleware = new RequestContextMiddleware(
            async ctx => await ctx.Response.WriteAsync("ok"),
            logger.Object);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "trace-xyz";

        await middleware.InvokeAsync(context);

        context.Response.Headers[RequestContextMiddleware.CorrelationIdHeader].ToString().ShouldBe("trace-xyz");
    }
}
