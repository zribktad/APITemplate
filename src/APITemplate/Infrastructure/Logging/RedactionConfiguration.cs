using APITemplate.Application.Options;

namespace APITemplate.Infrastructure.Logging;

public static class RedactionConfiguration
{
    public static string ResolveHmacKey(
        RedactionOptions options,
        Func<string, string?> getEnvironmentVariable)
    {
        var key = getEnvironmentVariable(options.HmacKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(key))
            return key;

        if (!string.IsNullOrWhiteSpace(options.HmacKey))
            return options.HmacKey;

        throw new InvalidOperationException(
            $"Missing redaction HMAC key. Set environment variable '{options.HmacKeyEnvironmentVariable}' or configure 'Redaction:HmacKey'.");
    }
}
