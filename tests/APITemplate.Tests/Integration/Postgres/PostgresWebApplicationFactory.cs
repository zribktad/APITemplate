using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using System.Security.Cryptography;
using System.Text;
using Testcontainers.PostgreSql;
using Xunit;

namespace APITemplate.Tests.Integration.Postgres;

public sealed class PostgresWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase($"apitemplate_tests_{Guid.NewGuid():N}")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithCleanUp(true)
        .Build();

    public Task InitializeAsync() => _postgresContainer.StartAsync();

    public new Task DisposeAsync() => _postgresContainer.DisposeAsync().AsTask();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var testRedactionHmacKey = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes("APITemplate.Tests.RedactionKey.Postgres")));

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgresContainer.GetConnectionString(),
                ["Jwt:Secret"] = "TestSuperSecretKeyThatIsAtLeast32Chars!",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:ExpirationMinutes"] = "60",
                ["SystemIdentity:DefaultActorId"] = "system",
                ["Bootstrap:Admin:Username"] = "admin",
                ["Bootstrap:Admin:Password"] = "admin",
                ["Bootstrap:Admin:Email"] = "admin@example.com",
                ["Bootstrap:Admin:IsPlatformAdmin"] = "true",
                ["Bootstrap:Tenant:Code"] = "default",
                ["Bootstrap:Tenant:Name"] = "Default Tenant",
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
                ["Redaction:HmacKeyEnvironmentVariable"] = "APITEMPLATE_REDACTION_HMAC_KEY",
                ["Redaction:HmacKey"] = testRedactionHmacKey,
                ["Redaction:KeyId"] = "1001"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // MongoDB is intentionally disabled in integration tests.
            services.RemoveAll(typeof(MongoDbContext));
            services.RemoveAll(typeof(IProductDataRepository));
            services.AddSingleton(new Mock<IProductDataRepository>().Object);
        });

        builder.UseEnvironment("Development");
    }
}
