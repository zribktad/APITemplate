using APITemplate.Application.Common.Contracts;

namespace APITemplate.Application.Features.ProductReview.DTOs;
public sealed record ProductReviewFilter(
    Guid? ProductId = null,
    Guid? UserId = null,
    int? MinRating = null,
    int? MaxRating = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    string? SortBy = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = 10) : PaginationFilter(PageNumber, PageSize), IDateRangeFilter, ISortableFilter;
