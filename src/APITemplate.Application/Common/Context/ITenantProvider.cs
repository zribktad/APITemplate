namespace APITemplate.Application.Common.Context;

public interface ITenantProvider
{
    Guid TenantId { get; }
    bool HasTenant { get; }
}
