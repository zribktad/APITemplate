using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

public interface ITenantRepository : IRepository<Tenant>
{
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
}
