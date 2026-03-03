using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;

namespace APITemplate.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove Npgsql provider registrations (required for .NET 9+)
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(AppDbContext));

            // Remove IDbContextOptionsConfiguration<> - critical for .NET 9/10
            var optionsConfigs = services
                .Where(d =>
                    d.ServiceType.IsGenericType &&
                    d.ServiceType.GetGenericTypeDefinition().FullName?
                        .Contains("IDbContextOptionsConfiguration") == true)
                .ToList();

            foreach (var d in optionsConfigs)
                services.Remove(d);

            // Re-register with InMemory provider
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            // MongoDB is not available in tests.
            // Remove MongoDbContext so the migration runner is skipped in UseDatabaseAsync.
            // Replace IProductDataRepository with a no-op mock so DI validation (ValidateOnBuild) passes.
            services.RemoveAll(typeof(MongoDbContext));
            services.RemoveAll(typeof(IProductDataRepository));
            services.AddSingleton(new Mock<IProductDataRepository>().Object);
        });

        builder.UseEnvironment("Development");
    }
}
