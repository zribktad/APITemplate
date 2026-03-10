namespace APITemplate.Application.Features.Category.DTOs;
public sealed record ProductCategoryStatsResponse(
    Guid CategoryId,
    string CategoryName,
    long ProductCount,
    decimal AveragePrice,
    long TotalReviews);
