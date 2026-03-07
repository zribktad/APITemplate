using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Options;
using APITemplate.Extensions;
using APITemplate.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Persistence;

public class PostgresRetryConfigurationTests
{
    [Fact]
    public void AddPersistence_BindsConfiguredPostgresRetryOptions()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["Persistence:PostgresRetry:Enabled"] = "true",
                ["Persistence:PostgresRetry:MaxRetryCount"] = "7",
                ["Persistence:PostgresRetry:MaxRetryDelaySeconds"] = "11"
            })
            .Build();

        services.AddLogging();
        services.AddSingleton<ITenantProvider, TestTenantProvider>();
        services.AddSingleton<IActorProvider, TestActorProvider>();
        services.AddPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PostgresRetryOptions>>().Value;

        options.Enabled.ShouldBeTrue();
        options.MaxRetryCount.ShouldBe(7);
        options.MaxRetryDelaySeconds.ShouldBe(11);
    }

    [Fact]
    public void AddPersistence_ConfiguresRetryingExecutionStrategyWhenEnabled()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["Persistence:PostgresRetry:Enabled"] = "true",
                ["Persistence:PostgresRetry:MaxRetryCount"] = "3",
                ["Persistence:PostgresRetry:MaxRetryDelaySeconds"] = "5"
            })
            .Build();

        services.AddLogging();
        services.AddSingleton<ITenantProvider, TestTenantProvider>();
        services.AddSingleton<IActorProvider, TestActorProvider>();
        services.AddPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Database.CreateExecutionStrategy().RetriesOnFailure.ShouldBeTrue();
    }

    [Fact]
    public void AddPersistence_UsesDefaultRetryOptionsWhenSectionMissing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
            })
            .Build();

        services.AddLogging();
        services.AddSingleton<ITenantProvider, TestTenantProvider>();
        services.AddSingleton<IActorProvider, TestActorProvider>();
        services.AddPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<PostgresRetryOptions>>().Value;

        options.Enabled.ShouldBeTrue();
        options.MaxRetryCount.ShouldBe(3);
        options.MaxRetryDelaySeconds.ShouldBe(5);
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid TenantId => Guid.Empty;
        public bool HasTenant => false;
    }

    private sealed class TestActorProvider : IActorProvider
    {
        public Guid ActorId => Guid.Empty;
    }
}
