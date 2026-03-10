using APITemplate.Application.Common.Validation;
using FluentValidation;

namespace APITemplate.Application.Features.Product.Validation;
public abstract class ProductRequestValidatorBase<T> : DataAnnotationsValidator<T>
    where T : class, IProductRequest
{
    protected ProductRequestValidatorBase()
    {
        // Cross-field rule: cannot be expressed via Data Annotations
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required for products priced above 1000.")
            .When(x => x.Price > 1000);
    }
}
