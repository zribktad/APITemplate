using APITemplate.Application.Common.Context;
using APITemplate.Infrastructure.Security;
using APITemplate.Application.Features.Category.Services;
using APITemplate.Application.Features.Product.Services;
using APITemplate.Application.Features.Product.Validation;
using APITemplate.Application.Features.ProductReview.Services;
using Asp.Versioning;
using FluentValidation;

namespace APITemplate.Extensions;

public static class ServiceCollectionExtensions
{
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
        services.AddValidatorsFromAssemblyContaining<CreateProductRequestValidator>();

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

}
