using APITemplate.Application.Common.Contracts;

namespace APITemplate.Application.Features.Product.DTOs;
public sealed record ProductFilter(
    string? Name = null,
    string? Description = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    string? SortBy = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = 10) : PaginationFilter(PageNumber, PageSize), IDateRangeFilter, ISortableFilter;
