using System.Net;
using System.Text.Json;
using APITemplate.Api.ExceptionHandling;
using APITemplate.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.ExceptionHandling;

public class ApiExceptionHandlerTests
{
    private readonly Mock<ILogger<ApiExceptionHandler>> _loggerMock = new();
    private readonly IProblemDetailsService _problemDetailsService;

    public ApiExceptionHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var extensions = context.ProblemDetails.Extensions;
                var errorCode = extensions.TryGetValue("errorCode", out var code) && code is string existingCode
                    ? existingCode
                    : ErrorCatalog.General.Unknown;

                extensions["traceId"] = context.HttpContext.TraceIdentifier;
                extensions["errorCode"] = errorCode;
                context.ProblemDetails.Type ??= $"https://api-template.local/errors/{errorCode}";
            };
        });
        _problemDetailsService = services.BuildServiceProvider().GetRequiredService<IProblemDetailsService>();
    }

    [Fact]
    public async Task TryHandleAsync_WhenAppException_ReturnsProblemDetailsWithErrorCode()
    {
        var context = CreateHttpContext();

        var handler = new ApiExceptionHandler(_loggerMock.Object, _problemDetailsService);
        var handled = await handler.TryHandleAsync(
            context,
            new NotFoundException("Product", Guid.Empty, ErrorCatalog.Reviews.ProductNotFoundForReview),
            CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        context.Response.ContentType.ShouldStartWith("application/problem+json");

        var body = await ReadJsonBody(context);
        body.GetProperty("status").GetInt32().ShouldBe((int)HttpStatusCode.NotFound);
        body.GetProperty("title").GetString().ShouldBe("Not Found");
        body.GetProperty("detail").GetString().ShouldBe($"Product with id '{Guid.Empty}' not found.");
        body.GetProperty("errorCode").GetString().ShouldBe(ErrorCatalog.Reviews.ProductNotFoundForReview);
        body.GetProperty("type").GetString().ShouldBe($"https://api-template.local/errors/{ErrorCatalog.Reviews.ProductNotFoundForReview}");
        body.GetProperty("traceId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TryHandleAsync_WhenValidationException_ReturnsCatalogErrorCode()
    {
        var context = CreateHttpContext();

        var handler = new ApiExceptionHandler(_loggerMock.Object, _problemDetailsService);
        var handled = await handler.TryHandleAsync(context, new ValidationException("validation failed"), CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);

        var body = await ReadJsonBody(context);
        body.GetProperty("status").GetInt32().ShouldBe((int)HttpStatusCode.BadRequest);
        body.GetProperty("title").GetString().ShouldBe("Bad Request");
        body.GetProperty("detail").GetString().ShouldBe("validation failed");
        body.GetProperty("errorCode").GetString().ShouldBe(ErrorCatalog.General.ValidationFailed);
    }

    [Fact]
    public async Task TryHandleAsync_WhenUnhandledException_Returns500WithGenericCode()
    {
        var context = CreateHttpContext();

        var handler = new ApiExceptionHandler(_loggerMock.Object, _problemDetailsService);
        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe((int)HttpStatusCode.InternalServerError);

        var body = await ReadJsonBody(context);
        body.GetProperty("title").GetString().ShouldBe("Internal Server Error");
        body.GetProperty("detail").GetString().ShouldBe("An unexpected error occurred.");
        body.GetProperty("errorCode").GetString().ShouldBe(ErrorCatalog.General.Unknown);
        body.GetProperty("type").GetString().ShouldBe($"https://api-template.local/errors/{ErrorCatalog.General.Unknown}");
    }

    [Fact]
    public async Task TryHandleAsync_WhenGraphQlPath_ReturnsFalse()
    {
        var context = CreateHttpContext();
        context.Request.Path = "/graphql";

        var handler = new ApiExceptionHandler(_loggerMock.Object, _problemDetailsService);
        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        handled.ShouldBeFalse();
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/test";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> ReadJsonBody(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await JsonDocument.ParseAsync(context.Response.Body);
        return json.RootElement.Clone();
    }
}
