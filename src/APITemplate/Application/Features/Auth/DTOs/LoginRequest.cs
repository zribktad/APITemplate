using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.Auth.DTOs;
public sealed record LoginRequest(
    [property: NotEmpty(ErrorMessage = "Username is required.")]
    string Username,

    [property: NotEmpty(ErrorMessage = "Password is required.")]
    string Password);
