using APITemplate.Application.Common.Validation;
using APITemplate.Application.Features.Tenant.DTOs;
using FluentValidation;

namespace APITemplate.Application.Features.Tenant.Validation;

public sealed class TenantFilterValidator : AbstractValidator<TenantFilter>
{
    public TenantFilterValidator()
    {
        Include(new PaginationFilterValidator());
        Include(new SortableFilterValidator<TenantFilter>(TenantSortFields.Map.AllowedNames));
    }
}
