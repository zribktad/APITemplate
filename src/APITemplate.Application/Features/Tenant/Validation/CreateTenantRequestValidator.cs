using APITemplate.Application.Common.Validation;
using APITemplate.Application.Features.Tenant.DTOs;

namespace APITemplate.Application.Features.Tenant.Validation;

public sealed class CreateTenantRequestValidator : DataAnnotationsValidator<CreateTenantRequest>;
