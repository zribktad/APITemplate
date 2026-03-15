using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.User.DTOs;

public sealed record RequestPasswordResetRequest(
    [NotEmpty] [MaxLength(320)] [EmailAddress] string Email);
