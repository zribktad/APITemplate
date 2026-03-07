using APITemplate.Domain.Entities;

namespace APITemplate.Application.Common.Options;

public sealed class SystemIdentityOptions
{
    public Guid DefaultActorId { get; init; } = AuditDefaults.SystemActorId;
}
