using APITemplate.Domain.Entities;
using Ardalis.Specification;

namespace APITemplate.Application.Features.User.Specifications;

public sealed class UserByUsernameSpecification : Specification<AppUser>
{
    public UserByUsernameSpecification(string normalizedUsername)
    {
        Query.Where(u => u.NormalizedUsername == normalizedUsername);
    }
}
