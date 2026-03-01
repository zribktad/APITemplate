using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace APITemplate.Infrastructure.Repositories;

public sealed class ProductReviewRepository : RepositoryBase<ProductReview>, IProductReviewRepository
{
    public ProductReviewRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public override async Task<IReadOnlyList<ProductReview>> GetAllAsync(CancellationToken ct = default)
    {
        return await DbContext.ProductReviews
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProductReview>> GetByProductIdAsync(Guid productId, CancellationToken ct = default)
    {
        return await DbContext.ProductReviews
            .AsNoTracking()
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }
}
