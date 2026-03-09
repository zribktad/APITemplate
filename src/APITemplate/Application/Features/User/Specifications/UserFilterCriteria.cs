using APITemplate.Application.Features.User.DTOs;
using APITemplate.Domain.Entities;
using Ardalis.Specification;

namespace APITemplate.Application.Features.User.Specifications;

internal static class UserFilterCriteria
{
    internal static void Apply(ISpecificationBuilder<AppUser> query, UserFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Username))
            query.Where(u => u.Username.Contains(filter.Username));

        if (!string.IsNullOrWhiteSpace(filter.Email))
            query.Where(u => u.Email.Contains(filter.Email));

        if (filter.IsActive.HasValue)
            query.Where(u => u.IsActive == filter.IsActive.Value);

        if (filter.Role.HasValue)
            query.Where(u => u.Role == filter.Role.Value);
    }
}
