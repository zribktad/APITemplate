using APITemplate.Application.Features.User.DTOs;
using APITemplate.Domain.Enums;
using FluentValidation;

namespace APITemplate.Application.Features.User.Validation;

public sealed class ChangeUserRoleRequestValidator : AbstractValidator<ChangeUserRoleRequest>
{
    public ChangeUserRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .IsInEnum()
            .WithMessage("Role must be a valid UserRole value.");
    }
}
