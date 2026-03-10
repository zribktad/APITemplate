namespace APITemplate.Application.Common.Security;

/// <summary>
/// Shared constants for the custom CSRF header contract used by <c>CsrfValidationMiddleware</c>.
/// </summary>
/// <remarks>
/// SPAs retrieve these values at runtime via <c>GET /api/v1/bff/csrf</c> and must send
/// <c>X-CSRF: 1</c> on every non-safe (mutating) request authenticated with a session cookie.
/// </remarks>
public static class CsrfConstants
{
    /// <summary>Name of the required anti-CSRF request header.</summary>
    public const string HeaderName = "X-CSRF";

    /// <summary>Expected value of the anti-CSRF header.</summary>
    public const string HeaderValue = "1";
}
