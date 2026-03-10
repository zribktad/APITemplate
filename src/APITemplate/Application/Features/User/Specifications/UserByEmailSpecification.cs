using APITemplate.Domain.Entities;
using Ardalis.Specification;

namespace APITemplate.Application.Features.User.Specifications;

public sealed class UserByEmailSpecification : Specification<AppUser>
{
    public UserByEmailSpecification(string email)
    {
        var normalizedEmail = AppUser.NormalizeEmail(email);
        Query.Where(u => u.NormalizedEmail == normalizedEmail);
    }
}
