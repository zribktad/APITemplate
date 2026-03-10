using APITemplate.Application.Common.Context;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Repositories;

public sealed class ProductDataLinkRepository : IProductDataLinkRepository
{
    private readonly AppDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public ProductDataLinkRepository(AppDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<IReadOnlyList<ProductDataLink>> ListByProductIdAsync(
        Guid productId,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = includeDeleted
            ? _dbContext.ProductDataLinks
                .IgnoreQueryFilters()
                .Where(link => link.TenantId == _tenantProvider.TenantId && link.ProductId == productId)
            : _dbContext.ProductDataLinks
                .Where(link => link.ProductId == productId);

        return await query.ToListAsync(ct);
    }

    public Task<bool> HasActiveLinksForProductDataAsync(Guid productDataId, CancellationToken ct = default) =>
        _dbContext.ProductDataLinks.AnyAsync(link => link.ProductDataId == productDataId, ct);

    public async Task SoftDeleteActiveLinksForProductDataAsync(Guid productDataId, CancellationToken ct = default)
    {
        var links = await _dbContext.ProductDataLinks
            .Where(link => link.ProductDataId == productDataId)
            .ToListAsync(ct);

        if (links.Count == 0)
            return;

        _dbContext.ProductDataLinks.RemoveRange(links);
    }
}
