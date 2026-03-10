using APITemplate.Domain.Entities;
using Ardalis.Specification;

namespace APITemplate.Application.Features.User.Specifications;

public sealed class UserByEmailSpecification : Specification<AppUser>
{
    public UserByEmailSpecification(string email)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();
        Query.Where(u => u.Email.ToUpper() == normalizedEmail);
    }
}
