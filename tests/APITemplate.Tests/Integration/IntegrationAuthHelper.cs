using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Enums;
using APITemplate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace APITemplate.Tests.Integration;

internal static class IntegrationAuthHelper
{
    public static async Task<string> LoginAndGetTokenAsync(
        HttpClient client,
        string username = "default\\admin",
        string password = "admin")
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { Username = username, Password = password });

        response.EnsureSuccessStatusCode();

        var loginJson = await response.Content.ReadFromJsonAsync<JsonElement>();
        return loginJson.GetProperty("accessToken").GetString()!;
    }

    public static async Task AuthenticateAsync(
        HttpClient client,
        string username = "default\\admin",
        string password = "admin")
    {
        var token = await LoginAndGetTokenAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<Guid> AuthenticateAndGetUserIdAsync(
        HttpClient client,
        string username = "default\\admin",
        string password = "admin")
    {
        var token = await LoginAndGetTokenAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var sub = jwt.Claims.First(c => c.Type == "sub").Value;
        return Guid.Parse(sub);
    }

    public static async Task<(Tenant Tenant, AppUser User)> SeedTenantUserAsync(
        IServiceProvider services,
        string username,
        string email,
        string password,
        bool userIsActive = true,
        bool tenantIsActive = true)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Empty,
            Code = $"tenant-{Guid.NewGuid():N}",
            Name = $"Tenant {username}",
            IsActive = tenantIsActive
        };

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Username = username,
            Email = email,
            PasswordHash = string.Empty,
            IsActive = userIsActive,
            Role = UserRole.TenantUser
        };

        var hasher = new PasswordHasher<AppUser>();
        user.PasswordHash = hasher.HashPassword(user, password);

        dbContext.Tenants.Add(tenant);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return (tenant, user);
    }
}
