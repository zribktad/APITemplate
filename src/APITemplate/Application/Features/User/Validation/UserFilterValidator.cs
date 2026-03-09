using APITemplate.Application.Common.Validation;
using APITemplate.Application.Features.User.DTOs;
using APITemplate.Domain.Enums;
using FluentValidation;

namespace APITemplate.Application.Features.User.Validation;

public sealed class UserFilterValidator : DataAnnotationsValidator<UserFilter>
{
    public UserFilterValidator()
    {
        Include(new SortableFilterValidator<UserFilter>(UserSortFields.Map.AllowedNames));

        RuleFor(x => x.Role)
            .IsInEnum()
            .When(x => x.Role.HasValue)
            .WithMessage("Role must be a valid UserRole value.");
    }
}
