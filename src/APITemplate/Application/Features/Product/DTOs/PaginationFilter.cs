namespace APITemplate.Application.Features.Product.DTOs;
public record PaginationFilter(
    int PageNumber = 1,
    int PageSize = 10);