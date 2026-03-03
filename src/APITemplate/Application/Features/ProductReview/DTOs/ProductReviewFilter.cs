namespace APITemplate.Application.Features.ProductReview.DTOs;
public sealed record ProductReviewFilter(
    Guid? ProductId = null,
    string? ReviewerName = null,
    int? MinRating = null,
    int? MaxRating = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    int PageNumber = 1,
    int PageSize = 10) : PaginationFilter(PageNumber, PageSize);
