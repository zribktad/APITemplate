using APITemplate.Api.GraphQL.Instrumentation;

namespace APITemplate.Extensions;

public static class GraphQLServiceCollectionExtensions
{
    public static IServiceCollection AddGraphQLConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<GraphQlExecutionMetricsListener>();

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
            .AddInstrumentation()
            .AddDiagnosticEventListener(sp => sp.GetRequiredService<GraphQlExecutionMetricsListener>())
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
