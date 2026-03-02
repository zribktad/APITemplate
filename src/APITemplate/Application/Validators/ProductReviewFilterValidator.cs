using APITemplate.Application.DTOs;
using FluentValidation;

namespace APITemplate.Application.Validators;

public sealed class ProductReviewFilterValidator : AbstractValidator<ProductReviewFilter>
{
    public ProductReviewFilterValidator()
    {
        Include(new PaginationFilterValidator());

        RuleFor(x => x.MinRating)
            .InclusiveBetween(1, 5).WithMessage("MinRating must be between 1 and 5.")
            .When(x => x.MinRating.HasValue);

        RuleFor(x => x.MaxRating)
            .InclusiveBetween(1, 5).WithMessage("MaxRating must be between 1 and 5.")
            .When(x => x.MaxRating.HasValue);

        RuleFor(x => x.MaxRating)
            .GreaterThanOrEqualTo(x => x.MinRating!.Value).WithMessage("MaxRating must be greater than or equal to MinRating.")
            .When(x => x.MinRating.HasValue && x.MaxRating.HasValue);

        RuleFor(x => x.CreatedTo)
            .GreaterThanOrEqualTo(x => x.CreatedFrom!.Value).WithMessage("CreatedTo must be greater than or equal to CreatedFrom.")
            .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue);
    }
}
