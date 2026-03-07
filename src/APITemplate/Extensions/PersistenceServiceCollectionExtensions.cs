using APITemplate.Application.Common.Options;
using APITemplate.Application.Features.ProductData.Services;
using APITemplate.Infrastructure.Health;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using APITemplate.Infrastructure.Repositories;
using APITemplate.Infrastructure.StoredProcedures;
using Kot.MongoDB.Migrations;
using Kot.MongoDB.Migrations.DI;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Extensions;

public static class PersistenceServiceCollectionExtensions
{
    public const string PostgresRetrySectionName = "Persistence:PostgresRetry";

    public static IServiceCollection AddPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;
        var retryOptions = configuration.GetSection(PostgresRetrySectionName).Get<PostgresRetryOptions>() ?? new PostgresRetryOptions();

        services.Configure<PostgresRetryOptions>(configuration.GetSection(PostgresRetrySectionName));

        services.AddDbContext<AppDbContext>(options =>
            ConfigurePostgresDbContext(options, connectionString, retryOptions));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductReviewRepository, ProductReviewRepository>();
        services.AddScoped<IStoredProcedureExecutor, StoredProcedureExecutor>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<AuthBootstrapSeeder>();
        services.AddScoped<ISoftDeleteCascadeRule, ProductSoftDeleteCascadeRule>();
        services.AddSingleton(TimeProvider.System);

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: ["database"]);

        return services;
    }

    internal static void ConfigurePostgresDbContext(
        DbContextOptionsBuilder options,
        string connectionString,
        PostgresRetryOptions retryOptions)
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            if (!retryOptions.Enabled)
                return;

            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: retryOptions.MaxRetryCount,
                maxRetryDelay: TimeSpan.FromSeconds(retryOptions.MaxRetryDelaySeconds),
                errorCodesToAdd: null);
        });
    }

    public static IServiceCollection AddMongoDB(
        this IServiceCollection services, IConfiguration configuration)
    {
        var mongoSettings = configuration.GetSection("MongoDB").Get<MongoDbSettings>()!;

        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDB"));
        services.AddSingleton<MongoDbContext>();
        services.AddScoped<IProductDataRepository, ProductDataRepository>();
        services.AddScoped<IProductDataService, ProductDataService>();

        services.AddMongoMigrations(
            mongoSettings.ConnectionString,
            new MigrationOptions(mongoSettings.DatabaseName),
            config => config.LoadMigrationsFromAssembly(typeof(PersistenceServiceCollectionExtensions).Assembly));

        services.AddHealthChecks()
            .AddCheck<MongoDbHealthCheck>("mongodb", tags: ["database"]);

        return services;
    }
}
