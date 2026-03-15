using APITemplate.Application.Common.Validation;
using APITemplate.Application.Features.User.DTOs;

namespace APITemplate.Application.Features.User.Validation;

public sealed class CreateUserRequestValidator : DataAnnotationsValidator<CreateUserRequest>
{
}
