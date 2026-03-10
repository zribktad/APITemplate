using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;

namespace APITemplate.Infrastructure.Repositories;

public sealed class ProductReviewRepository : RepositoryBase<ProductReview>, IProductReviewRepository
{
    public ProductReviewRepository(AppDbContext dbContext) : base(dbContext)
    {
    }
}
