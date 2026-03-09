using APITemplate.Application.Common.Validation;
using APITemplate.Application.Features.User.DTOs;
using FluentValidation;

namespace APITemplate.Application.Features.User.Validation;

public sealed class CreateUserRequestValidator : DataAnnotationsValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Password)
            .Must(p => p is not null && p.Any(char.IsUpper))
            .WithMessage("Password must contain at least one uppercase letter.")
            .Must(p => p is not null && p.Any(char.IsDigit))
            .WithMessage("Password must contain at least one digit.")
            .Must(p => p is not null && p.Any(c => !char.IsLetterOrDigit(c)))
            .WithMessage("Password must contain at least one special character.");
    }
}
