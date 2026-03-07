using System.Text.Json.Serialization;

namespace APITemplate.Application.Features.ProductData.DTOs;

[JsonDerivedType(typeof(ImageProductDataResponse), "image")]
[JsonDerivedType(typeof(VideoProductDataResponse), "video")]
public abstract record ProductDataResponse
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? Format { get; init; }
    public long? FileSizeBytes { get; init; }
}

public sealed record ImageProductDataResponse : ProductDataResponse
{
    public int Width { get; init; }
    public int Height { get; init; }
}

public sealed record VideoProductDataResponse : ProductDataResponse
{
    public int DurationSeconds { get; init; }
    public string? Resolution { get; init; }
}
