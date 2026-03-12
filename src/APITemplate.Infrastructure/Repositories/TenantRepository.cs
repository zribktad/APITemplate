using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using Ardalis.Specification;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Repositories;

public sealed class TenantRepository : RepositoryBase<Tenant>, ITenantRepository
{
    public TenantRepository(AppDbContext dbContext)
        : base(dbContext) { }

    private IQueryable<Tenant> UnfilteredTenants => AppDb.Tenants.IgnoreQueryFilters(["Tenant"]);

    protected override IQueryable<Tenant> ApplySpecification(
        ISpecification<Tenant> specification,
        bool evaluateCriteriaOnly = false
    )
    {
        return SpecificationEvaluator.GetQuery(
            UnfilteredTenants,
            specification,
            evaluateCriteriaOnly
        );
    }

    protected override IQueryable<TResult> ApplySpecification<TResult>(
        ISpecification<Tenant, TResult> specification
    )
    {
        return SpecificationEvaluator.GetQuery(UnfilteredTenants, specification);
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
    {
        return UnfilteredTenants.AnyAsync(t => t.Code == code, ct);
    }
}
