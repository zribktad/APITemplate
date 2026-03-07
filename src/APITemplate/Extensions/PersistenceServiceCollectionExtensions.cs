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
    public const string TransactionsSectionName = "Persistence:Transactions";

    public static IServiceCollection AddPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;
        var transactionDefaults = configuration.GetSection(TransactionsSectionName).Get<TransactionDefaultsOptions>() ?? new TransactionDefaultsOptions();

        services.Configure<TransactionDefaultsOptions>(configuration.GetSection(TransactionsSectionName));

        services.AddDbContext<AppDbContext>(options =>
            ConfigurePostgresDbContext(options, connectionString, transactionDefaults));

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
        TransactionDefaultsOptions transactionDefaults)
    {
        _ = transactionDefaults;
        options.UseNpgsql(connectionString);
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
