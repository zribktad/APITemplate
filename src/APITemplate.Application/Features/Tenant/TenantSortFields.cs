using APITemplate.Application.Common.Sorting;
using TenantEntity = APITemplate.Domain.Entities.Tenant;

namespace APITemplate.Application.Features.Tenant;

public static class TenantSortFields
{
    public static readonly SortField Code = new("code");
    public static readonly SortField Name = new("name");
    public static readonly SortField CreatedAt = new("createdAt");

    public static readonly SortFieldMap<TenantEntity> Map = new SortFieldMap<TenantEntity>()
        .Add(Code, t => t.Code)
        .Add(Name, t => t.Name)
        .Add(CreatedAt, t => t.Audit.CreatedAtUtc)
        .Default(t => t.Audit.CreatedAtUtc);
}
