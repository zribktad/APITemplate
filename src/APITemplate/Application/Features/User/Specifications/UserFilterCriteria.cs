using APITemplate.Application.Features.User.DTOs;
using APITemplate.Domain.Entities;
using Ardalis.Specification;

namespace APITemplate.Application.Features.User.Specifications;

internal static class UserFilterCriteria
{
    internal static void Apply(ISpecificationBuilder<AppUser> query, UserFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Username))
        {
            var normalizedUsername = AppUser.NormalizeUsername(filter.Username);
            query.Where(u => u.NormalizedUsername.Contains(normalizedUsername));
        }

        if (!string.IsNullOrWhiteSpace(filter.Email))
        {
            var normalizedEmail = filter.Email.Trim().ToUpperInvariant();
            query.Where(u => u.Email.ToUpper() == normalizedEmail);
        }

        if (filter.IsActive.HasValue)
            query.Where(u => u.IsActive == filter.IsActive.Value);

        if (filter.Role.HasValue)
            query.Where(u => u.Role == filter.Role.Value);
    }
}
