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

    public override async Task<Tenant?> GetByIdAsync<TId>(
        TId id,
        CancellationToken cancellationToken = default
    )
        where TId : default
    {
        if (id is not Guid guid)
            throw new ArgumentException(
                $"Expected Guid but received {typeof(TId).Name}.",
                nameof(id)
            );

        return await UnfilteredTenants.FirstOrDefaultAsync(t => t.Id == guid, cancellationToken);
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
    {
        return AppDb
            .Tenants.IgnoreQueryFilters(["Tenant", "SoftDelete"])
            .AnyAsync(t => t.Code == code, ct);
    }
}
