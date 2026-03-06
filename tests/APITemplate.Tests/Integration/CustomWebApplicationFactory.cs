using APITemplate.Infrastructure.Persistence;
using APITemplate.Tests.Integration.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace APITemplate.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var config = TestConfigurationHelper.GetBaseConfiguration();
            config["ReverseProxy:Routes:bff-proxy:ClusterId"] = "api-self";
            config["ReverseProxy:Routes:bff-proxy:Match:Path"] = "/bff/proxy/{**catch-all}";
            config["ReverseProxy:Clusters:api-self:Destinations:self:Address"] = "http://localhost/";
            configBuilder.AddInMemoryCollection(config);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(AppDbContext));

            var optionsConfigs = services
                .Where(d =>
                    d.ServiceType.IsGenericType &&
                    d.ServiceType.GetGenericTypeDefinition().FullName?
                        .Contains("IDbContextOptionsConfiguration") == true)
                .ToList();

            foreach (var d in optionsConfigs)
                services.Remove(d);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            TestServiceHelper.MockMongoServices(services);
            TestServiceHelper.RemoveExternalHealthChecks(services);
            TestServiceHelper.ConfigureTestAuthentication(services);
        });

        builder.UseEnvironment("Development");
    }
}
