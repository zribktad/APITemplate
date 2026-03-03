namespace APITemplate.Application.Features.ProductData.DTOs;
public sealed class ProductDataResponse
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }

    // Image-specific
    public int? Width { get; init; }
    public int? Height { get; init; }

    // Video-specific
    public int? DurationSeconds { get; init; }
    public string? Resolution { get; init; }

    // Shared
    public string? Format { get; init; }
    public long? FileSizeBytes { get; init; }
}
