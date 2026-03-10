using APITemplate.Application.Features.User.DTOs;
using APITemplate.Domain.Entities;
using Ardalis.Specification;

namespace APITemplate.Application.Features.User.Specifications;

public sealed class UserCountSpecification : Specification<AppUser>
{
    public UserCountSpecification(UserFilter filter)
    {
        UserFilterCriteria.Apply(Query, filter);
        Query.AsNoTracking();
    }
}
