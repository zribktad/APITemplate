namespace APITemplate.Api.GraphQL.Models;

public sealed class ProductReviewQueryInput
{
    public Guid? ProductId { get; init; }
    public Guid? UserId { get; init; }
    public int? MinRating { get; init; }
    public int? MaxRating { get; init; }
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
