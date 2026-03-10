using System.Threading.RateLimiting;
using APITemplate.Api.Cache;
using APITemplate.Api.ExceptionHandling;
using APITemplate.Api.OpenApi;
using APITemplate.Application.Common.Options;
using APITemplate.Infrastructure.Observability;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;

namespace APITemplate.Extensions;

public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers core API services (controllers, OpenAPI, ProblemDetails) and
    /// exception handling dependencies.
    /// </summary>
    /// <remarks>
    /// This method only registers exception handling services in DI
    /// (including <see cref="ApiExceptionHandler"/>). Runtime exception interception
    /// is activated later by calling <c>app.UseExceptionHandler()</c> in the middleware pipeline.
    /// </remarks>
    public static IServiceCollection AddApiFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddProblemDetailsAndExceptionHandling()
            .AddOpenApiDocumentation()
            .AddRateLimiting(configuration)
            .AddValkeyAndDataProtection(configuration)
            .AddOutputCaching(configuration);

        return services;
    }

    private static IServiceCollection AddProblemDetailsAndExceptionHandling(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddProblemDetails(ApiProblemDetailsOptions.Configure);

        // Registers the handler in DI; middleware activation happens in UseApiPipeline via app.UseExceptionHandler().
        services.AddExceptionHandler<ApiExceptionHandler>();
        return services;
    }

    private static IServiceCollection AddOpenApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeDocumentTransformer>();
            options.AddDocumentTransformer<HealthCheckOpenApiDocumentTransformer>();
            options.AddDocumentTransformer<ProblemDetailsOpenApiTransformer>();
            options.AddOperationTransformer<AuthorizationResponsesOperationTransformer>();
        });

        return services;
    }

    private static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection("RateLimiting:Fixed"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Per-client fixed window rate limiter. Partition key priority:
        //   1. JWT username (authenticated users)
        //   2. Remote IP address (anonymous users)
        //   3. "anonymous" fallback (shared bucket when neither is available)
        // IConfigureOptions is used so values are resolved from DI at first request,
        // not captured at registration time — this allows tests to override RateLimitingOptions.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                var endpoint = HttpRouteResolver.Resolve(context.HttpContext);
                ApiMetrics.RecordRateLimitRejection(
                    CachePolicyNames.RateLimitPolicy,
                    context.HttpContext.Request.Method,
                    endpoint);
                return ValueTask.CompletedTask;
            };
        });

        services.AddSingleton<IConfigureOptions<RateLimiterOptions>>(sp =>
        {
            var rateLimitOpts = sp.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
            return new ConfigureOptions<RateLimiterOptions>(o =>
                o.AddPolicy(CachePolicyNames.RateLimitPolicy, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.Identity?.Name
                            ?? httpContext.Connection.RemoteIpAddress?.ToString()
                            ?? "anonymous",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = rateLimitOpts.PermitLimit,
                            Window = TimeSpan.FromMinutes(rateLimitOpts.WindowMinutes)
                        })));
        });

        return services;
    }

    private static IServiceCollection AddValkeyAndDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Output Cache with optional Valkey backing store.
        // When Valkey:ConnectionString is configured, cached responses are stored in Valkey
        // so all application instances share the same cache. Without it, falls back to in-memory.
        // Each policy defines an expiration time and a tag used for targeted invalidation
        // via IOutputCacheStore.EvictByTagAsync() in controllers after mutations (Create/Update/Delete).
        var valkeySection = configuration.GetSection("Valkey");
        var valkeyConnectionString = valkeySection.GetValue<string>("ConnectionString");

        if (!string.IsNullOrEmpty(valkeyConnectionString))
        {
            services.AddOptions<ValkeyOptions>()
                .Bind(valkeySection)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Lazy singleton: the TCP connection is established on first use, not at registration time.
            // This keeps startup fast and allows tests to replace Redis services before the
            // connection is ever attempted.
            var lazyMultiplexer = new Lazy<IConnectionMultiplexer>(
                () => ConnectionMultiplexer.Connect(valkeyConnectionString));
            services.AddSingleton<IConnectionMultiplexer>(_ => lazyMultiplexer.Value);

            services.AddStackExchangeRedisOutputCache(options =>
            {
                options.ConnectionMultiplexerFactory = () => Task.FromResult(lazyMultiplexer.Value);
                options.InstanceName = "ApiTemplate:OutputCache:";
            });

            services.AddDataProtection()
                .SetApplicationName("APITemplate")
                .PersistKeysToStackExchangeRedis(
                    () => lazyMultiplexer.Value.GetDatabase(),
                    "DataProtection:Keys");

            services.AddStackExchangeRedisCache(options =>
            {
                options.ConnectionMultiplexerFactory = () => Task.FromResult(lazyMultiplexer.Value);
                options.InstanceName = "ApiTemplate:Session:";
            });

            services.AddHealthChecks()
                .AddRedis(valkeyConnectionString, name: "valkey", tags: ["cache"]);
        }
        else
        {
            Log.Warning("Valkey:ConnectionString is not configured — using in-memory output cache. " +
                        "This is not suitable for multi-instance deployments");
            services.AddDistributedMemoryCache();
        }

        return services;
    }

    private static IServiceCollection AddOutputCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<TenantAwareOutputCachePolicy>();
        services.AddScoped<IOutputCacheInvalidationService, OutputCacheInvalidationService>();

        var cachingSection = configuration.GetSection("Caching");
        services.AddOutputCache(options =>
        {
            options.AddBasePolicy(builder => builder.NoCache());
            options.AddPolicy(CachePolicyNames.Products, builder => builder
                .AddPolicy<TenantAwareOutputCachePolicy>()
                .Expire(TimeSpan.FromSeconds(cachingSection.GetValue<int>("ProductsExpirationSeconds", 30)))
                .Tag(CachePolicyNames.Products));
            options.AddPolicy(CachePolicyNames.Categories, builder => builder
                .AddPolicy<TenantAwareOutputCachePolicy>()
                .Expire(TimeSpan.FromSeconds(cachingSection.GetValue<int>("CategoriesExpirationSeconds", 60)))
                .Tag(CachePolicyNames.Categories));
            options.AddPolicy(CachePolicyNames.Reviews, builder => builder
                .AddPolicy<TenantAwareOutputCachePolicy>()
                .Expire(TimeSpan.FromSeconds(cachingSection.GetValue<int>("ReviewsExpirationSeconds", 30)))
                .Tag(CachePolicyNames.Reviews));
        });

        return services;
    }
}
