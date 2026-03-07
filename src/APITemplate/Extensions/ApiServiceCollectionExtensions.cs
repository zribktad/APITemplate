using System.Threading.RateLimiting;
using APITemplate.Api.Cache;
using APITemplate.Api.ExceptionHandling;
using APITemplate.Api.Filters;
using APITemplate.Api.OpenApi;
using APITemplate.Application.Common.Options;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

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
        services.AddControllers(options =>
        {
            options.Filters.Add<FluentValidationActionFilter>();
        });
        services.AddProblemDetails(ApiProblemDetailsOptions.Configure);

        // Registers the handler in DI; middleware activation happens in UseApiPipeline via app.UseExceptionHandler().
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeDocumentTransformer>();
            options.AddDocumentTransformer<HealthCheckOpenApiDocumentTransformer>();
            options.AddDocumentTransformer<ProblemDetailsOpenApiTransformer>();
            options.AddOperationTransformer<AuthorizationResponsesOperationTransformer>();
        });

        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection("RateLimiting:Fixed"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var rateLimitOpts = configuration.GetSection("RateLimiting:Fixed").Get<RateLimitingOptions>()
            ?? new RateLimitingOptions();
        // Per-client fixed window rate limiter. Partition key priority:
        //   1. JWT username (authenticated users)
        //   2. Remote IP address (anonymous users)
        //   3. "anonymous" fallback (shared bucket when neither is available)
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(CachePolicyNames.RateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.Identity?.Name
                        ?? httpContext.Connection.RemoteIpAddress?.ToString()
                        ?? "anonymous",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOpts.PermitLimit,
                        Window = TimeSpan.FromMinutes(rateLimitOpts.WindowMinutes)
                    }));
        });

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

            services.AddStackExchangeRedisOutputCache(options =>
            {
                options.Configuration = valkeyConnectionString;
                options.InstanceName = "ApiTemplate:OutputCache:";
            });

            services.AddHealthChecks()
                .AddRedis(valkeyConnectionString, name: "valkey", tags: ["cache"]);
        }
        else
        {
            Log.Warning("Valkey:ConnectionString is not configured — using in-memory output cache. " +
                        "This is not suitable for multi-instance deployments");
        }

        services.AddSingleton<TenantAwareOutputCachePolicy>();

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
