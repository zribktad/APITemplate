using APITemplate.Application.Features.User.DTOs;
using APITemplate.Application.Features.User.Mappings;
using APITemplate.Domain.Entities;
using Ardalis.Specification;

namespace APITemplate.Application.Features.User.Specifications;

public sealed class UserFilterSpecification : Specification<AppUser, UserResponse>
{
    public UserFilterSpecification(UserFilter filter)
    {
        UserFilterCriteria.Apply(Query, filter);
        Query.AsNoTracking();

        UserSortFields.Map.ApplySort(Query, filter.SortBy, filter.SortDirection);

        Query.Select(UserMappings.Projection);

        Query.Skip((filter.PageNumber - 1) * filter.PageSize)
             .Take(filter.PageSize);
    }
}
