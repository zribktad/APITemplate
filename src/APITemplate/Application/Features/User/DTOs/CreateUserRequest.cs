using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.User.DTOs;

public sealed record CreateUserRequest(
    [NotEmpty] [MaxLength(100)] string Username,
    [NotEmpty] [MaxLength(320)] [EmailAddress] string Email,
    [NotEmpty] [MinLength(8)] [MaxLength(128)] string Password);
