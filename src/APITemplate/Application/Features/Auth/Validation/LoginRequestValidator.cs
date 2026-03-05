using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.Auth.Validation;
public sealed class LoginRequestValidator : DataAnnotationsValidator<LoginRequest>;
