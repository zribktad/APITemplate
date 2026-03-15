using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Behaviors;
using APITemplate.Api.Cache;
using APITemplate.Infrastructure.Security;
using APITemplate.Application.Features.Product;
using APITemplate.Application.Features.Product.Validation;
using Asp.Versioning;
using FluentValidation;
using MediatR;

namespace APITemplate.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, HttpTenantProvider>();
        services.AddScoped<IActorProvider, HttpActorProvider>();
services.AddValidatorsFromAssemblyContaining<CreateProductRequestValidator>();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<CreateProductCommand>();
            cfg.RegisterServicesFromAssemblyContaining<CacheInvalidationNotificationHandler>();
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
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

}
