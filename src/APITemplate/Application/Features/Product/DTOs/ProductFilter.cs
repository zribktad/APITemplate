namespace APITemplate.Application.Features.Product.DTOs;
public sealed record ProductFilter(
    string? Name = null,
    string? Description = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    int PageNumber = 1,
    int PageSize = 10) : PaginationFilter(PageNumber, PageSize);
