using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Resilience;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.Product.Repositories;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Health;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.Auditing;
using APITemplate.Infrastructure.Persistence.EntityNormalization;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using APITemplate.Infrastructure.Repositories;
using APITemplate.Infrastructure.Security;
using APITemplate.Infrastructure.StoredProcedures;
using Kot.MongoDB.Migrations;
using Kot.MongoDB.Migrations.DI;
using Microsoft.EntityFrameworkCore;
using Polly;

namespace APITemplate.Extensions;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString(
            ConfigurationSections.DefaultConnection
        )!;

        var transactionDefaults =
            configuration.SectionFor<TransactionDefaultsOptions>().Get<TransactionDefaultsOptions>()
            ?? new TransactionDefaultsOptions();

        services.Configure<TransactionDefaultsOptions>(
            configuration.SectionFor<TransactionDefaultsOptions>()
        );

        services.AddDbContext<AppDbContext>(options =>
            ConfigurePostgresDbContext(options, connectionString, transactionDefaults)
        );

        // Repositories (data access)
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductDataLinkRepository, ProductDataLinkRepository>();
        services.AddScoped<IProductReviewRepository, ProductReviewRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ITenantInvitationRepository, TenantInvitationRepository>();

        // Infrastructure / persistence helpers
        services.AddScoped<IStoredProcedureExecutor, StoredProcedureExecutor>();
        services.AddScoped<IDbTransactionProvider, EfCoreTransactionProvider>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Auditing / normalization / soft delete behavior
        services.AddSingleton<IEntityNormalizationService, AppUserEntityNormalizationService>();
        services.AddSingleton<IAuditableEntityStateManager, AuditableEntityStateManager>();
        services.AddSingleton<ISoftDeleteProcessor, SoftDeleteProcessor>();
        services.AddScoped<ISoftDeleteCascadeRule, ProductSoftDeleteCascadeRule>();
        services.AddScoped<ISoftDeleteCascadeRule, TenantSoftDeleteCascadeRule>();

        // Application services / initialization
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();
        services.AddScoped<AuthBootstrapSeeder>();

        // System services
        services.AddSingleton(TimeProvider.System);

        services
            .AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: ["database"]);

        return services;
    }

    internal static void ConfigurePostgresDbContext(
        DbContextOptionsBuilder options,
        string connectionString,
        TransactionDefaultsOptions transactionDefaults
    )
    {
        _ = transactionDefaults;
        options.UseNpgsql(connectionString);
    }

    public static IServiceCollection AddMongoDB(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var mongoSettings = configuration
            .GetSection(ConfigurationSections.MongoDB)
            .Get<MongoDbSettings>()!;

        services.Configure<MongoDbSettings>(
            configuration.GetSection(ConfigurationSections.MongoDB)
        );
        services.AddSingleton<MongoDbContext>();
        services.AddScoped<IProductDataRepository, ProductDataRepository>();

        services.AddResiliencePipeline(
            ResiliencePipelineKeys.MongoProductDataDelete,
            builder =>
            {
                builder.AddRetry(
                    new()
                    {
                        MaxRetryAttempts = 3,
                        BackoffType = DelayBackoffType.Exponential,
                        Delay = TimeSpan.FromSeconds(1),
                        UseJitter = true,
                    }
                );
            }
        );

        services.AddMongoMigrations(
            mongoSettings.ConnectionString,
            new MigrationOptions(mongoSettings.DatabaseName),
            config =>
                config.LoadMigrationsFromAssembly(
                    typeof(PersistenceServiceCollectionExtensions).Assembly
                )
        );

        services.AddHealthChecks().AddCheck<MongoDbHealthCheck>("mongodb", tags: ["database"]);

        return services;
    }
}
