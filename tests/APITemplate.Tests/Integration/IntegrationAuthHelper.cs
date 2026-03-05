using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using APITemplate.Application.Common.Security;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Enums;
using APITemplate.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace APITemplate.Tests.Integration;

internal static class IntegrationAuthHelper
{
    public static string GenerateTestToken(
        Guid userId,
        Guid tenantId,
        string role = nameof(UserRole.PlatformAdmin))
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(CustomClaimTypes.TenantId, tenantId.ToString()),
            new Claim("groups", role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        return GenerateTestTokenWithClaims(claims);
    }

    public static string GenerateTestTokenWithClaims(
        Claim[] claims,
        DateTime? expires = null,
        DateTime? notBefore = null)
    {
        var token = new JwtSecurityToken(
            issuer: TestAuthKeys.Issuer,
            audience: TestAuthKeys.Audience,
            claims: claims,
            notBefore: notBefore,
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: TestAuthKeys.SigningCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void Authenticate(
        HttpClient client,
        Guid userId,
        Guid tenantId,
        string role = nameof(UserRole.PlatformAdmin))
    {
        var token = GenerateTestToken(userId, tenantId, role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static Guid AuthenticateAndGetUserId(
        HttpClient client,
        Guid tenantId,
        string role = nameof(UserRole.PlatformAdmin))
    {
        var userId = Guid.NewGuid();
        Authenticate(client, userId, tenantId, role);
        return userId;
    }

    public static async Task<Tenant> SeedTenantAsync(
        IServiceProvider services,
        string? tenantCode = null,
        string? tenantName = null)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Empty,
            Code = tenantCode ?? $"tenant-{Guid.NewGuid():N}",
            Name = tenantName ?? "Test Tenant",
            IsActive = true
        };

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        return tenant;
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
            PasswordHash = "not-used-with-authentik",
            IsActive = userIsActive,
            Role = UserRole.TenantUser
        };

        dbContext.Tenants.Add(tenant);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return (tenant, user);
    }

    /// <summary>
    /// Seeds a tenant and authenticates the client with a test token. Returns the user ID.
    /// </summary>
    public static async Task<Guid> AuthenticateAndGetUserIdAsync(
        HttpClient client,
        IServiceProvider services,
        string role = nameof(UserRole.PlatformAdmin))
    {
        var tenant = await SeedTenantAsync(services);
        return AuthenticateAndGetUserId(client, tenant.Id, role);
    }
}
