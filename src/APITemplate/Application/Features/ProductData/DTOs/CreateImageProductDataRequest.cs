using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Features.ProductData.DTOs;
public sealed record CreateImageProductDataRequest(
    [property: NotEmpty(ErrorMessage = "Title is required.")]
    [property: MaxLength(200, ErrorMessage = "Title must not exceed 200 characters.")]
    string Title,

    [property: MaxLength(1000, ErrorMessage = "Description must not exceed 1000 characters.")]
    string? Description,

    [property: Range(1, int.MaxValue, ErrorMessage = "Width must be greater than zero.")]
    int Width,

    [property: Range(1, int.MaxValue, ErrorMessage = "Height must be greater than zero.")]
    int Height,

    [property: NotEmpty(ErrorMessage = "Format is required.")]
    [property: AllowedValues("jpg", "png", "gif", "webp", ErrorMessage = "Format must be one of: jpg, png, gif, webp.")]
    string Format,

    [property: Range(1, long.MaxValue, ErrorMessage = "FileSizeBytes must be greater than zero.")]
    long FileSizeBytes);
