using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Common.DTOs;
public record PaginationFilter(
    [Range(1, int.MaxValue, ErrorMessage = "PageNumber must be greater than or equal to 1.")]
    int PageNumber = 1,

    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
    int PageSize = 20)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}
