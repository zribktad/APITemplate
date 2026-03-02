using System.Text;
using APITemplate.Application.Interfaces;
using APITemplate.Application.Services;
using APITemplate.Application.Validators;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.Repositories;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace APITemplate.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductReviewRepository, ProductReviewRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql", tags: ["database"]);

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductReviewService, ProductReviewService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IUserService, UserService>();
        services.AddValidatorsFromAssemblyContaining<CreateProductRequestValidator>();
        services.AddFluentValidationAutoValidation();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var key = Encoding.UTF8.GetBytes(jwtSettings["Secret"]!);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };
            });

        services.AddAuthorization();

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
            .AddAuthorization()
            .AddProjections()
            .AddFiltering()
            .AddSorting()
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
