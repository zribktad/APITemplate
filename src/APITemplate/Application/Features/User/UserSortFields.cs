using APITemplate.Application.Common.Sorting;
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Features.User;

public static class UserSortFields
{
    public static readonly SortField Username = new("username");
    public static readonly SortField Email = new("email");
    public static readonly SortField CreatedAt = new("createdAt");

    public static readonly SortFieldMap<AppUser> Map = new SortFieldMap<AppUser>()
        .Add(Username, u => u.Username)
        .Add(Email, u => u.Email)
        .Add(CreatedAt, u => u.Audit.CreatedAtUtc)
        .Default(u => u.Audit.CreatedAtUtc);
}
