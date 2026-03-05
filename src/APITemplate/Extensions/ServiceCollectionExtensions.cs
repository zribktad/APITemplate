using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.Auth.Interfaces;
using APITemplate.Application.Features.Category.Services;
using APITemplate.Application.Features.Product.Services;
using APITemplate.Application.Features.Product.Validation;
using APITemplate.Application.Features.ProductData.Services;
using APITemplate.Application.Features.ProductReview.Services;
using APITemplate.Domain.Enums;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Health;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Persistence.SoftDelete;
using APITemplate.Infrastructure.Repositories;
using APITemplate.Infrastructure.Security;
using APITemplate.Infrastructure.StoredProcedures;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
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
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
        }

        services.AddOptions<AuthentikOptions>()
            .Bind(configuration.GetSection("Authentik"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SystemIdentityOptions>()
            .Bind(configuration.GetSection("SystemIdentity"))
            .ValidateDataAnnotations()
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.DefaultActorId),
                "SystemIdentity:DefaultActorId is required")
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
        services.AddHttpClient<IAuthenticationProxy, AuthentikAuthenticationProxy>();
        services.AddValidatorsFromAssemblyContaining<CreateProductRequestValidator>();
        services.AddFluentValidationAutoValidation();

        return services;
    }

    public static IServiceCollection AddAuthentikAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthentikOptions>>((options, authentikAccessor) =>
            {
                var authentik = authentikAccessor.Value;

                options.Authority = authentik.Authority;
                options.RequireHttpsMetadata = !authentik.Authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                options.Audience = authentik.ClientId;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = "preferred_username",
                    RoleClaimType = authentik.RoleClaimType
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (!TenantClaimValidator.HasValidTenantClaim(context.Principal))
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

        services.AddHttpClient<AuthentikHealthCheck>();
        services.AddHealthChecks()
            .AddCheck<AuthentikHealthCheck>("authentik", tags: ["identity"]);

        return services;
    }

    public static IServiceCollection AddBffAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BffOptions>()
            .Bind(configuration.GetSection("Bff"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register schemes with no-op config — actual configuration is deferred below via
        // AddOptions<T>().Configure<IOptions<BffOptions>>() so that test factories can override
        // BffOptions values BEFORE they're resolved. Eager configuration here would read
        // empty/default values that tests haven't had a chance to override yet.
        services.AddAuthentication()
            .AddCookie(BffAuthenticationSchemes.Cookie)
            .AddOpenIdConnect(BffAuthenticationSchemes.Oidc, _ => { });

        services.AddOptions<CookieAuthenticationOptions>(BffAuthenticationSchemes.Cookie)
            .Configure<IOptions<BffOptions>>((options, bffAccessor) =>
            {
                var bff = bffAccessor.Value;
                options.Cookie.Name = bff.CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(bff.SessionTimeoutMinutes);
                options.SlidingExpiration = true;

                options.Events.OnValidatePrincipal = context =>
                {
                    if (!TenantClaimValidator.HasValidTenantClaim(context.Principal))
                    {
                        context.RejectPrincipal();
                    }
                    return Task.CompletedTask;
                };
            });

        services.AddOptions<OpenIdConnectOptions>(BffAuthenticationSchemes.Oidc)
            .Configure<IOptions<BffOptions>>((options, bffAccessor) =>
            {
                var bff = bffAccessor.Value;
                options.Authority = bff.Authority;
                options.RequireHttpsMetadata = !bff.Authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
                options.ClientId = bff.ClientId;
                options.ClientSecret = bff.ClientSecret;
                options.ResponseType = "code";
                options.UsePkce = true;
                options.SaveTokens = true;
                options.SignInScheme = BffAuthenticationSchemes.Cookie;
                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = true;

                options.Scope.Clear();
                foreach (var scope in bff.Scopes)
                    options.Scope.Add(scope);

                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (!TenantClaimValidator.HasValidTenantClaim(context.Principal))
                            context.Fail("Missing required tenant_id claim.");

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = ".APITemplate.Antiforgery";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.HttpOnly = true;
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
