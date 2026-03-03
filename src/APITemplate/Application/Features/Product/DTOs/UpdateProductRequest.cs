using System.ComponentModel.DataAnnotations;

namespace APITemplate.Application.Features.Product.DTOs;
public sealed record UpdateProductRequest(
    [property: NotEmpty(ErrorMessage = "Product name is required.")]
    [property: MaxLength(200, ErrorMessage = "Product name must not exceed 200 characters.")]
    string Name,

    string? Description,

    [property: Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
    decimal Price,

    Guid? CategoryId = null) : IProductRequest;
