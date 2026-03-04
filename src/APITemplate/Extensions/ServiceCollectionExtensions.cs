using System.Text;
using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Security;
using APITemplate.Domain.Interfaces;
using APITemplate.Domain.Enums;
using APITemplate.Application.Features.Auth.Services;
using APITemplate.Application.Features.Category.Services;
using APITemplate.Application.Features.Product.Services;
using APITemplate.Application.Features.Product.Validation;
using APITemplate.Application.Features.ProductData.Services;
using APITemplate.Application.Features.ProductReview.Services;
using APITemplate.Infrastructure.Health;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using APITemplate.Infrastructure.Repositories;
using APITemplate.Infrastructure.StoredProcedures;
using APITemplate.Infrastructure.Security;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Kot.MongoDB.Migrations;
using Kot.MongoDB.Migrations.DI;

namespace APITemplate.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthenticationOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection("Cors"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var corsOrigins = configuration.GetSection("Cors:AllowedOrigins")
            .Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .ToArray();

        if (corsOrigins?.Length > 0)
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(corsOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });
        }

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection("Jwt"))
            .ValidateDataAnnotations()
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Secret) && o.Secret.Length >= 32,
                "Jwt secret too short")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Jwt issuer/audience is required")
            .ValidateOnStart();

        services.AddOptions<SystemIdentityOptions>()
            .Bind(configuration.GetSection("SystemIdentity"))
            .ValidateDataAnnotations()
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.DefaultActorId),
                "SystemIdentity:DefaultActorId is required")
            .ValidateOnStart();

        services.AddOptions<BootstrapAdminOptions>()
            .Bind(configuration.GetSection("Bootstrap:Admin"))
            .ValidateDataAnnotations()
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Username) && !string.IsNullOrWhiteSpace(o.Password),
                "Bootstrap admin username/password is required")
            .Validate(
                o => !environment.IsProduction() ||
                     (HasNonEmptyEnvironmentVariable("Bootstrap__Admin__Username") &&
                      HasNonEmptyEnvironmentVariable("Bootstrap__Admin__Password")),
                "In Production, Bootstrap admin credentials must be provided via environment variables: Bootstrap__Admin__Username and Bootstrap__Admin__Password")
            .Validate(
                o => !environment.IsProduction() || !IsKnownDefaultBootstrapCredential(o),
                "In Production, Bootstrap admin credentials cannot use known default placeholders")
            .ValidateOnStart();

        services.AddOptions<BootstrapTenantOptions>()
            .Bind(configuration.GetSection("Bootstrap:Tenant"))
            .ValidateDataAnnotations()
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Code) && !string.IsNullOrWhiteSpace(o.Name),
                "Bootstrap tenant code/name is required")
            .ValidateOnStart();

        return services;
    }

    private static bool HasNonEmptyEnvironmentVariable(string variableName) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variableName));

    private static bool IsKnownDefaultBootstrapCredential(BootstrapAdminOptions options)
    {
        var username = options.Username.Trim();
        var password = options.Password.Trim();

        var isDefaultUser = string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase);
        var isDefaultPassword =
            string.Equals(password, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(password, "change-me", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(password, "changeme", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(password, "password", StringComparison.OrdinalIgnoreCase);

        return isDefaultUser && isDefaultPassword;
    }

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
        // Register explicit soft-delete cascade rules (aggregate-specific behavior).
        services.AddScoped<ISoftDeleteCascadeRule, ProductSoftDeleteCascadeRule>();

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: ["database"]);

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, HttpTenantProvider>();
        services.AddScoped<IActorProvider, HttpActorProvider>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductQueryService, ProductQueryService>();
        services.AddScoped<IProductReviewService, ProductReviewService>();
        services.AddScoped<IProductReviewQueryService, ProductReviewQueryService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IUserService, UserService>();
        services.AddValidatorsFromAssemblyContaining<CreateProductRequestValidator>();
        services.AddFluentValidationAutoValidation();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptionsAccessor) =>
            {
                var jwt = jwtOptionsAccessor.Value;
                var key = Encoding.UTF8.GetBytes(jwt.Secret);

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var hasTenantClaim = context.Principal?.HasClaim(
                            c => c.Type == CustomClaimTypes.TenantId
                                 && Guid.TryParse(c.Value, out var tenantId)
                                 && tenantId != Guid.Empty) == true;

                        if (!hasTenantClaim)
                            context.Fail("Missing required tenant_id claim.");

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.PlatformAdminOnly,
                policy => policy.RequireRole(UserRole.PlatformAdmin.ToString()));
        });

        return services;
    }

    public static IServiceCollection AddApiVersioningConfiguration(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

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
            config => config.LoadMigrationsFromAssembly(typeof(ServiceCollectionExtensions).Assembly));

        services.AddHealthChecks()
            .AddCheck<MongoDbHealthCheck>("mongodb", tags: ["database"]);

        return services;
    }

    public static IServiceCollection AddGraphQLConfiguration(this IServiceCollection services)
    {
        services
            .AddGraphQLServer()
            .AddQueryType<Api.GraphQL.Queries.ProductQueries>()
            .AddTypeExtension<Api.GraphQL.Queries.ProductReviewQueries>()
            .AddMutationType<Api.GraphQL.Mutations.ProductMutations>()
            .AddTypeExtension<Api.GraphQL.Mutations.ProductReviewMutations>()
            .AddType<Api.GraphQL.Types.ProductType>()
            .AddType<Api.GraphQL.Types.ProductReviewType>()
            .AddDataLoader<Api.GraphQL.DataLoaders.ProductReviewsByProductDataLoader>()
            .AddAuthorization()
            // Keep disabled for now: resolvers return DTO pages/services, not IQueryable.
            // Use AddProjections/AddFiltering/AddSorting when GraphQL fields expose IQueryable
            // with [UseProjection]/[UseFiltering]/[UseSorting] and you want SQL pushdown handled by HotChocolate.
            .ModifyPagingOptions(o =>
            {
                o.MaxPageSize = 100;
                o.DefaultPageSize = 20;
                o.IncludeTotalCount = true;
            })
            .AddMaxExecutionDepthRule(5);

        return services;
    }
}
