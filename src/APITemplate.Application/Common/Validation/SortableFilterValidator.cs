using APITemplate.Application.Common.Contracts;
using FluentValidation;

namespace APITemplate.Application.Common.Validation;
public sealed class SortableFilterValidator<T> : AbstractValidator<T>
    where T : ISortableFilter
{
    public SortableFilterValidator(IReadOnlyCollection<string> allowedSortFields)
    {
        RuleFor(x => x.SortBy)
            .Must(s => s is null || allowedSortFields.Any(f => f.Equals(s, StringComparison.OrdinalIgnoreCase)))
            .WithMessage($"SortBy must be one of: {string.Join(", ", allowedSortFields)}.");

        RuleFor(x => x.SortDirection)
            .Must(s => s is null || s.Equals("asc", StringComparison.OrdinalIgnoreCase)
                                 || s.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortDirection must be one of: asc, desc.");
    }
}
