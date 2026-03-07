using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using APITemplate.Application.Common.Security;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Enums;
using APITemplate.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace APITemplate.Tests.Integration;

internal static class IntegrationAuthHelper
{
    internal static readonly RSA RsaKey = RSA.Create(2048);

    internal static readonly RsaSecurityKey SecurityKey = new(RsaKey);

    private static readonly SigningCredentials SigningCredentials =
        new(SecurityKey, SecurityAlgorithms.RsaSha256);

    public static string CreateTestToken(
        Guid? userId = null,
        Guid? tenantId = null,
        string? username = null,
        UserRole role = UserRole.PlatformAdmin)
    {
        var id = userId ?? Guid.NewGuid();
        var tenant = tenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

        var claims = new List<Claim>
        {
            new("sub", id.ToString()),
            new("preferred_username", username ?? "admin"),
            new(ClaimTypes.Email, $"{username ?? "admin"}@example.com"),
            new(CustomClaimTypes.TenantId, tenant.ToString()),
            new(ClaimTypes.Role, role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "http://localhost:8180/realms/api-template",
            audience: "api-template",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: SigningCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void Authenticate(
        HttpClient client,
        Guid? userId = null,
        Guid? tenantId = null,
        string? username = null,
        UserRole role = UserRole.PlatformAdmin)
    {
        var token = CreateTestToken(userId, tenantId, username, role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static Guid AuthenticateAndGetUserId(
        HttpClient client,
        Guid? tenantId = null,
        string? username = null,
        UserRole role = UserRole.PlatformAdmin)
    {
        var userId = Guid.NewGuid();
        Authenticate(client, userId, tenantId, username, role);
        return userId;
    }

    public static async Task<(Tenant Tenant, AppUser User)> SeedTenantUserAsync(
        IServiceProvider services,
        string username,
        string email,
        string password,
        bool userIsActive = true,
        bool tenantIsActive = true,
        CancellationToken ct = default)
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
            Role = UserRole.User
        };

        var hasher = new PasswordHasher<AppUser>();
        user.PasswordHash = hasher.HashPassword(user, password);

        dbContext.Tenants.Add(tenant);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(ct);

        return (tenant, user);
    }
}
