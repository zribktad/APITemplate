using APITemplate.Application.DTOs;
using FluentValidation;

namespace APITemplate.Application.Validators;

public sealed class CreateProductReviewRequestValidator : AbstractValidator<CreateProductReviewRequest>
{
    public CreateProductReviewRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("ProductId is required.");

        RuleFor(x => x.ReviewerName)
            .NotEmpty().WithMessage("Reviewer name is required.")
            .MaximumLength(100).WithMessage("Reviewer name must not exceed 100 characters.");

        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 5).WithMessage("Rating must be between 1 and 5.");
    }
}
