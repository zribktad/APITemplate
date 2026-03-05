using APITemplate.Application.Common.Contracts;
using FluentValidation;

namespace APITemplate.Application.Common.Validation;
public sealed class DateRangeFilterValidator<T> : AbstractValidator<T>
    where T : IDateRangeFilter
{
    public DateRangeFilterValidator()
    {
        RuleFor(x => x.CreatedTo)
            .GreaterThanOrEqualTo(x => x.CreatedFrom!.Value)
            .WithMessage("CreatedTo must be greater than or equal to CreatedFrom.")
            .When(x => x.CreatedFrom.HasValue && x.CreatedTo.HasValue);
    }
}
