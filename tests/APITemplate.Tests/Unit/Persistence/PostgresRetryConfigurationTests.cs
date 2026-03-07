using System.Data;
using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Options;
using APITemplate.Extensions;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Persistence;

public class PostgresRetryConfigurationTests
{
    [Fact]
    public void AddPersistence_BindsConfiguredTransactionDefaults()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["Persistence:Transactions:IsolationLevel"] = "Serializable",
                ["Persistence:Transactions:TimeoutSeconds"] = "45",
                ["Persistence:Transactions:RetryEnabled"] = "true",
                ["Persistence:Transactions:RetryCount"] = "7",
                ["Persistence:Transactions:RetryDelaySeconds"] = "11"
            })
            .Build();

        services.AddLogging();
        services.AddSingleton<ITenantProvider, TestTenantProvider>();
        services.AddSingleton<IActorProvider, TestActorProvider>();
        services.AddPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<TransactionDefaultsOptions>>().Value;

        options.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
        options.TimeoutSeconds.ShouldBe(45);
        options.RetryEnabled.ShouldBeTrue();
        options.RetryCount.ShouldBe(7);
        options.RetryDelaySeconds.ShouldBe(11);
    }

    [Fact]
    public void AddPersistence_UsesDefaultTransactionSettingsWhenSectionMissing()
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
        var options = provider.GetRequiredService<IOptions<TransactionDefaultsOptions>>().Value;

        options.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
        options.TimeoutSeconds.ShouldBe(30);
        options.RetryEnabled.ShouldBeTrue();
        options.RetryCount.ShouldBe(3);
        options.RetryDelaySeconds.ShouldBe(5);
    }

    [Fact]
    public void ConfigurePostgresDbContext_EnablesRetryingExecutionStrategyWhenConfigured()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["Persistence:Transactions:RetryEnabled"] = "true",
                ["Persistence:Transactions:RetryCount"] = "4",
                ["Persistence:Transactions:RetryDelaySeconds"] = "9"
            })
            .Build();

        services.AddLogging();
        services.AddSingleton<ITenantProvider, TestTenantProvider>();
        services.AddSingleton<IActorProvider, TestActorProvider>();
        services.AddPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Database.CreateExecutionStrategy().ShouldBeOfType<NpgsqlRetryingExecutionStrategy>();
        dbContext.Database.CreateExecutionStrategy().RetriesOnFailure.ShouldBeTrue();
    }

    [Fact]
    public void ConfigurePostgresDbContext_DisablesProviderRetriesWhenConfigured()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["Persistence:Transactions:RetryEnabled"] = "false"
            })
            .Build();

        services.AddLogging();
        services.AddSingleton<ITenantProvider, TestTenantProvider>();
        services.AddSingleton<IActorProvider, TestActorProvider>();
        services.AddPersistence(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Database.CreateExecutionStrategy().ShouldBeOfType<NonRetryingExecutionStrategy>();
        dbContext.Database.CreateExecutionStrategy().RetriesOnFailure.ShouldBeFalse();
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
