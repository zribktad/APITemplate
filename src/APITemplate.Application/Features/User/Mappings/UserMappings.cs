using System.Linq.Expressions;
using APITemplate.Application.Features.User.DTOs;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Features.User.Mappings;

public static class UserMappings
{
    public static readonly Expression<Func<AppUser, UserResponse>> Projection =
        u => new UserResponse(
            u.Id,
            u.Username,
            u.Email,
            u.IsActive,
            u.Role,
            u.Audit.CreatedAtUtc);

    private static readonly Func<AppUser, UserResponse> CompiledProjection = Projection.Compile();

    public static UserResponse ToResponse(this AppUser user) => CompiledProjection(user);
}
