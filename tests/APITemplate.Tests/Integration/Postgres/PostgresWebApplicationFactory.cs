using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Tests.Integration.Helpers;
using APITemplate.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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

    public ValueTask InitializeAsync() => new(_postgresContainer.StartAsync());

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var config = TestConfigurationHelper.GetBaseConfiguration("APITemplate.Tests.RedactionKey.Postgres");
            config["ConnectionStrings:DefaultConnection"] = _postgresContainer.GetConnectionString();
            configBuilder.AddInMemoryCollection(config);
        });

        builder.ConfigureTestServices(services =>
        {
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

            var connectionString = _postgresContainer.GetConnectionString();
            using var bootstrapProvider = services.BuildServiceProvider();
            var transactionDefaults = bootstrapProvider.GetRequiredService<IOptions<TransactionDefaultsOptions>>().Value;
            services.AddDbContext<AppDbContext>(options =>
                PersistenceServiceCollectionExtensions.ConfigurePostgresDbContext(options, connectionString, transactionDefaults));

            TestServiceHelper.RemoveExternalHealthChecks(services);

            services.AddHealthChecks()
                .AddNpgSql(connectionString, name: "postgresql", tags: ["database"]);

            TestServiceHelper.MockMongoServices(services);
            TestServiceHelper.ReplaceOutputCacheWithInMemory(services);
            TestServiceHelper.ConfigureTestAuthentication(services);
        });

        builder.UseEnvironment("Development");
    }
}
