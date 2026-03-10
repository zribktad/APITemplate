using APITemplate.Application.Features.User.DTOs;
using APITemplate.Application.Features.User.Mappings;
using APITemplate.Domain.Entities;
using Ardalis.Specification;

namespace APITemplate.Application.Features.User.Specifications;

public sealed class UserByIdSpecification : Specification<AppUser, UserResponse>
{
    public UserByIdSpecification(Guid id)
    {
        Query.Where(u => u.Id == id)
            .Select(UserMappings.Projection);
    }
}
