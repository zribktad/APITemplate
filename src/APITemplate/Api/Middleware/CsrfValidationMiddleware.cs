using APITemplate.Application.Common.Security;
using Microsoft.AspNetCore.Diagnostics;

namespace APITemplate.Api.Middleware;

public sealed class CsrfValidationMiddleware(RequestDelegate next, IProblemDetailsService problemDetailsService)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method) ||
            HttpMethods.IsHead(context.Request.Method) ||
            HttpMethods.IsOptions(context.Request.Method))
        {
            await next(context);
            return;
        }

        var isCookieAuthenticated = context.User.Identities
            .Any(i => i.AuthenticationType == BffAuthenticationSchemes.Cookie);

        if (!isCookieAuthenticated)
        {
            await next(context);
            return;
        }

        if (context.Request.Headers.TryGetValue(CsrfConstants.HeaderName, out var value) &&
            value == CsrfConstants.HeaderValue)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = $"Cookie-authenticated requests must include the '{CsrfConstants.HeaderName}: {CsrfConstants.HeaderValue}' header."
            }
        });
    }
}
