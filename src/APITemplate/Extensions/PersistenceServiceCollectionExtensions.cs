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
    public static IServiceCollection AddPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductReviewRepository, ProductReviewRepository>();
        services.AddScoped<IStoredProcedureExecutor, StoredProcedureExecutor>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<AuthBootstrapSeeder>();
        services.AddScoped<ISoftDeleteCascadeRule, ProductSoftDeleteCascadeRule>();

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: ["database"]);

        return services;
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
