using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace APITemplate.Infrastructure.Security;

public sealed class BffTokenTransformProvider : ITransformProvider
{
    public void ValidateRoute(TransformRouteValidationContext context)
    {
    }

    public void ValidateCluster(TransformClusterValidationContext context)
    {
    }

    public void Apply(TransformBuilderContext context)
    {
        if (context.Route.RouteId != BffProxyConstants.RouteId)
            return;

        context.AddRequestTransform(async transformContext =>
        {
            var accessToken = await transformContext.HttpContext
                .GetTokenAsync(BffProxyConstants.AccessTokenName);

            if (!string.IsNullOrEmpty(accessToken))
            {
                transformContext.ProxyRequest.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
            }
        });
    }
}
