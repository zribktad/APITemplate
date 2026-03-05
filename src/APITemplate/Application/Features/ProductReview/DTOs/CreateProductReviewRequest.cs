using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.ProductReview.DTOs;
public sealed record CreateProductReviewRequest(
    [property: NotEmpty(ErrorMessage = "ProductId is required.")]
    Guid ProductId,

    string? Comment,

    [property: Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    int Rating);
