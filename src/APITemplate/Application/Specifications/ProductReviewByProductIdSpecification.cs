using Ardalis.Specification;
using APITemplate.Application.DTOs;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Specifications;

public sealed class ProductReviewByProductIdSpecification : Specification<ProductReview, ProductReviewResponse>
{
    public ProductReviewByProductIdSpecification(Guid productId)
    {
        Query.Where(r => r.ProductId == productId)
             .OrderByDescending(r => r.CreatedAt)
             .Select(r => new ProductReviewResponse(r.Id, r.ProductId, r.ReviewerName, r.Comment, r.Rating, r.CreatedAt));
    }
}
