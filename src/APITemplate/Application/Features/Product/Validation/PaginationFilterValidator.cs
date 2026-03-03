using FluentValidation;

namespace APITemplate.Application.Features.Product.Validation;
public sealed class PaginationFilterValidator : AbstractValidator<PaginationFilter>
{
    public PaginationFilterValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");
    }
}