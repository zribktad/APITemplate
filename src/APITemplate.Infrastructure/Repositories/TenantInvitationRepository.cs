using APITemplate.Domain.Entities;
using APITemplate.Domain.Enums;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Repositories;

public sealed class TenantInvitationRepository
    : RepositoryBase<TenantInvitation>,
        ITenantInvitationRepository
{
    public TenantInvitationRepository(AppDbContext dbContext)
        : base(dbContext) { }

    public Task<TenantInvitation?> GetValidByTokenHashAsync(
        string tokenHash,
        CancellationToken ct = default
    ) =>
        AppDb.TenantInvitations.FirstOrDefaultAsync(
            i => i.TokenHash == tokenHash && i.Status == InvitationStatus.Pending,
            ct
        );

    public Task<bool> HasPendingInvitationAsync(
        string normalizedEmail,
        CancellationToken ct = default
    ) =>
        AppDb.TenantInvitations.AnyAsync(
            i => i.NormalizedEmail == normalizedEmail && i.Status == InvitationStatus.Pending,
            ct
        );
}
