using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
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

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

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
            // Remove eagerly-captured Npgsql registrations so the container connection string is used.
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(AppDbContext));

            var optionsConfigs = services
                .Where(d =>
                    d.ServiceType.IsGenericType &&
                    d.ServiceType.GetGenericTypeDefinition().FullName?
                        .Contains("IDbContextOptionsConfiguration") == true)
                .ToList();

            foreach (var d in optionsConfigs)
                services.Remove(d);

            // Re-register with the test container's connection string.
            var connectionString = _postgresContainer.GetConnectionString();
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Replace the health check that was registered with the default connection string.
            var healthCheckDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("HealthCheck") == true)
                .ToList();

            foreach (var d in healthCheckDescriptors)
                services.Remove(d);

            services.AddHealthChecks()
                .AddNpgSql(connectionString, name: "postgresql", tags: ["database"]);

            // MongoDB is intentionally disabled in integration tests.
            services.RemoveAll(typeof(MongoDbContext));
            services.RemoveAll(typeof(IProductDataRepository));
            services.AddSingleton(new Mock<IProductDataRepository>().Object);
        });

        builder.UseEnvironment("Development");
    }
}
