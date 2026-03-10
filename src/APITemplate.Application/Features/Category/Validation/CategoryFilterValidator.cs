using APITemplate.Application.Common.Validation;
using FluentValidation;

namespace APITemplate.Application.Features.Category.Validation;

public sealed class CategoryFilterValidator : AbstractValidator<CategoryFilter>
{
    public CategoryFilterValidator()
    {
        Include(new PaginationFilterValidator());
        Include(new SortableFilterValidator<CategoryFilter>(CategorySortFields.Map.AllowedNames));
    }
}
