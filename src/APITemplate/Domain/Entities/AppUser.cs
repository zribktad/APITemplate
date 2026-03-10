using APITemplate.Domain.Enums;

namespace APITemplate.Domain.Entities;

public sealed class AppUser : IAuditableTenantEntity
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public string NormalizedUsername { get; set; } = string.Empty;
    public required string Email { get; set; }
    public string NormalizedEmail { get; set; } = string.Empty;
    public required string PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public UserRole Role { get; set; } = UserRole.User;

    public Tenant Tenant { get; set; } = null!;

    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }

    public static string NormalizeUsername(string username) => username.Trim().ToUpperInvariant();
    public static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}
