using APITemplate.Api.Authorization;
using APITemplate.Application.Common.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace APITemplate.Extensions;

public static class AuthorizationBuilderExtensions
{
    public static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder builder)
    {
        foreach (var permission in Permission.All)
        {
            builder.AddPolicy(permission, policy =>
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, BffAuthenticationSchemes.Cookie)
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(permission)));
        }

        return builder;
    }
}
