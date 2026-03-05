using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Features.Product.DTOs;
public record PaginationFilter(
    [property: Range(1, int.MaxValue, ErrorMessage = "PageNumber must be greater than or equal to 1.")]
    int PageNumber = 1,

    [property: Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
    int PageSize = 10);
