using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Features.ProductData.DTOs;
public sealed record CreateVideoProductDataRequest(
    [property: NotEmpty(ErrorMessage = "Title is required.")]
    [property: MaxLength(200, ErrorMessage = "Title must not exceed 200 characters.")]
    string Title,

    [property: MaxLength(1000, ErrorMessage = "Description must not exceed 1000 characters.")]
    string? Description,

    [property: Range(1, int.MaxValue, ErrorMessage = "DurationSeconds must be greater than zero.")]
    int DurationSeconds,

    [property: NotEmpty(ErrorMessage = "Resolution is required.")]
    [property: AllowedValues("720p", "1080p", "4K", ErrorMessage = "Resolution must be one of: 720p, 1080p, 4K.")]
    string Resolution,

    [property: NotEmpty(ErrorMessage = "Format is required.")]
    [property: AllowedValues("mp4", "avi", "mkv", ErrorMessage = "Format must be one of: mp4, avi, mkv.")]
    string Format,

    [property: Range(1, long.MaxValue, ErrorMessage = "FileSizeBytes must be greater than zero.")]
    long FileSizeBytes);
