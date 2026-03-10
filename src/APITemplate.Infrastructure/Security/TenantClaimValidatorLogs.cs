using APITemplate.Infrastructure.Logging;

namespace APITemplate.Infrastructure.Security;

internal static partial class TenantClaimValidatorLogs
{
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "[{Scheme}] Token validated but no identity found")]
    public static partial void TokenValidatedNoIdentity(
        this ILogger logger,
        string scheme);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "[{Scheme}] Token validated with {ClaimCount} claims: {Claims}")]
    public static partial void TokenValidatedWithClaims(
        this ILogger logger,
        string scheme,
        int claimCount,
        [SensitiveData] string claims);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "[{Scheme}] Authenticated user={User}, tenant={TenantId}, roles=[{Roles}]")]
    public static partial void UserAuthenticated(
        this ILogger logger,
        string scheme,
        [PersonalData] string? user,
        [SensitiveData] string? tenantId,
        string roles);
}
