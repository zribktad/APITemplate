using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Application.Common.Context;
using APITemplate.Infrastructure.Persistence;
using APITemplate.Infrastructure.StoredProcedures;

namespace APITemplate.Infrastructure.Repositories;

public sealed class CategoryRepository : RepositoryBase<Category>, ICategoryRepository
{
    private readonly IStoredProcedureExecutor _spExecutor;
    private readonly ITenantProvider _tenantProvider;

    public CategoryRepository(
        AppDbContext dbContext,
        IStoredProcedureExecutor spExecutor,
        ITenantProvider tenantProvider)
        : base(dbContext)
    {
        _spExecutor = spExecutor;
        _tenantProvider = tenantProvider;
    }

    public Task<ProductCategoryStats?> GetStatsByIdAsync(Guid categoryId, CancellationToken ct = default)
    {
        // Stored procedures bypass EF global query filters, so tenant must be passed explicitly for DB-side isolation.
        return _spExecutor.QueryFirstAsync(new GetProductCategoryStatsProcedure(categoryId, _tenantProvider.TenantId), ct);
    }
}
