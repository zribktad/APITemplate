using System.Security.Cryptography;
using System.Text;

namespace APITemplate.Tests.Integration.Helpers;

internal static class TestConfigurationHelper
{
    internal static Dictionary<string, string?> GetBaseConfiguration(string hmacKeySeed = "APITemplate.Tests.RedactionKey")
    {
        var testRedactionHmacKey = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(hmacKeySeed)));

        return new Dictionary<string, string?>
        {
            ["Keycloak:realm"] = "api-template",
            ["Keycloak:auth-server-url"] = "http://localhost:8180/",
            ["Keycloak:resource"] = "api-template",
            ["Keycloak:credentials:secret"] = "test-secret",
            ["SystemIdentity:DefaultActorId"] = "system",
            ["Bootstrap:Tenant:Code"] = "default",
            ["Bootstrap:Tenant:Name"] = "Default Tenant",
            ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
            ["Redaction:HmacKeyEnvironmentVariable"] = "APITEMPLATE_REDACTION_HMAC_KEY",
            ["Redaction:HmacKey"] = testRedactionHmacKey,
            ["Redaction:KeyId"] = "1001"
        };
    }
}
