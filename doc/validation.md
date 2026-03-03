# How to Add Input Validation (FluentValidation)

This guide explains how to add validation rules using **FluentValidation 11** — the validation library used throughout this project. Validation is triggered automatically for all controller inputs before the action method runs.

---

## Overview

The validation pipeline is:

```
HTTP Request body/query
  → FluentValidation (auto-validation via FluentValidation.AspNetCore)
  → If invalid: 400 Bad Request with structured error JSON
  → If valid: controller action method runs
```

Registration is done once in `ServiceCollectionExtensions.AddApplicationServices()`:

```csharp
services.AddValidatorsFromAssemblyContaining<CreateProductRequestValidator>();
services.AddFluentValidationAutoValidation();
```

Every `AbstractValidator<T>` in the assembly is discovered and registered automatically — no explicit registration per validator is needed.

---

## Step 1 – Create a Simple Validator

Place validators in `src/APITemplate/Application/Validators/`. Name them `<RequestTypeName>Validator.cs`.

**`src/APITemplate/Application/Validators/CreateOrderRequestValidator.cs`**

```csharp
using FluentValidation;

namespace APITemplate.Application.Validators;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");

        RuleFor(x => x.TotalAmount)
            .GreaterThan(0).WithMessage("TotalAmount must be greater than zero.")
            .LessThanOrEqualTo(1_000_000).WithMessage("TotalAmount must not exceed 1,000,000.");

        RuleFor(x => x.DeliveryAddress)
            .NotEmpty().WithMessage("DeliveryAddress is required.")
            .MaximumLength(500).WithMessage("DeliveryAddress must not exceed 500 characters.");
    }
}
```

---

## Step 2 – Cross-Field (Conditional) Rules

Use `.When()` to apply rules only under certain conditions. Use the validator against multiple fields with `.Must()` for cross-field comparisons:

```csharp
public sealed class CreateShipmentRequestValidator : AbstractValidator<CreateShipmentRequest>
{
    public CreateShipmentRequestValidator()
    {
        // Rule only applies when the order exceeds a threshold
        RuleFor(x => x.InsuranceValue)
            .GreaterThan(0).WithMessage("Insurance value required for high-value shipments.")
            .When(x => x.OrderValue > 5000);

        // Cross-field date range validation
        RuleFor(x => x.DeliveryDeadline)
            .GreaterThan(x => x.PickupDate).WithMessage("DeliveryDeadline must be after PickupDate.")
            .When(x => x.PickupDate != default);

        // Cross-field rule from the project: description required for expensive products
        // See ProductRequestValidatorBase.cs for the real example:
        // RuleFor(x => x.Description)
        //     .NotEmpty().WithMessage("Description required for products priced above 1000.")
        //     .When(x => x.Price > 1000);
    }
}
```

---

## Step 3 – Shared Base Validator

When multiple request types share the same fields and rules, extract a base validator:

```csharp
// Application/Validators/ProductRequestValidatorBase.cs (existing pattern)
public abstract class ProductRequestValidatorBase<T> : AbstractValidator<T>
    where T : IProductRequest
{
    protected ProductRequestValidatorBase()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required for products priced above 1000.")
            .When(x => x.Price > 1000);
    }
}

// CreateProductRequestValidator delegates entirely to the base:
public sealed class CreateProductRequestValidator
    : ProductRequestValidatorBase<CreateProductRequest>;

// UpdateProductRequestValidator does the same:
public sealed class UpdateProductRequestValidator
    : ProductRequestValidatorBase<UpdateProductRequest>;
```

---

## Step 4 – Including Another Validator

Use `Include()` to compose validators without inheritance. This is useful for shared pagination/filter rules:

```csharp
// Application/Validators/PaginationFilterValidator.cs (existing)
public sealed class PaginationFilterValidator : AbstractValidator<PaginationFilter>
{
    public PaginationFilterValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

// ProductFilterValidator.cs includes the base pagination rules:
public sealed class ProductFilterValidator : AbstractValidator<ProductFilter>
{
    public ProductFilterValidator()
    {
        Include(new PaginationFilterValidator());   // ← compose

        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).When(x => x.MinPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(x => x.MinPrice!.Value)
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue);
    }
}
```

---

## Step 5 – Collection Item Validation

To validate each element in a list, use `RuleForEach`:

```csharp
public sealed class CreateBulkOrderRequestValidator : AbstractValidator<CreateBulkOrderRequest>
{
    public CreateBulkOrderRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one order item is required.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Sku).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).GreaterThan(0);
        });
    }
}
```

---

## Step 6 – Async Validation Rules

For rules that need a database lookup, use `MustAsync`:

```csharp
public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator(ICustomerRepository customerRepo)
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .MustAsync(async (id, ct) =>
                await customerRepo.ExistsAsync(id, ct))
            .WithMessage("Customer does not exist.");
    }
}
```

> **Note:** Async validators require the injected dependency to be registered as a scoped or transient service (matching the validator lifetime).

---

## How Validation Errors Are Returned

When any rule fails, FluentValidation returns HTTP **400 Bad Request** with this structure:

```json
{
  "errors": {
    "TotalAmount": [
      "TotalAmount must be greater than zero."
    ],
    "DeliveryAddress": [
      "DeliveryAddress is required."
    ]
  },
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400
}
```

The `GlobalExceptionHandlerMiddleware` handles domain-level `ValidationException` (thrown manually in service code) and returns HTTP 400 as well.

---

## Manually Throwing a Validation Error in Service Code

For business rule violations discovered after initial input validation (e.g., duplicate SKU in the database), throw `ValidationException`:

```csharp
// Domain/Exceptions/ValidationException.cs
public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct)
{
    var existing = await _repo.GetBySkuAsync(request.Sku, ct);
    if (existing is not null)
        throw new ValidationException($"An order for SKU '{request.Sku}' already exists.");

    // ... proceed with creation
}
```

---

## Checklist

- [ ] Create `<RequestType>Validator.cs` in `Application/Validators/`
- [ ] Inherit from `AbstractValidator<YourRequestType>`
- [ ] Add `RuleFor` calls for each property
- [ ] For shared rules, create a base validator or use `Include()`
- [ ] No registration needed — validators are discovered automatically from the assembly

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Application/Validators/` | All validator classes |
| `Application/Validators/ProductRequestValidatorBase.cs` | Base validator example |
| `Application/Validators/ProductFilterValidator.cs` | `Include()` composition example |
| `Application/Validators/CreateProductReviewRequestValidator.cs` | Typical standalone validator |
| `Domain/Exceptions/ValidationException.cs` | Domain-level validation exception |
| `Api/Middleware/GlobalExceptionHandlerMiddleware.cs` | Catches `ValidationException` → 400 |
| `Extensions/ServiceCollectionExtensions.cs` | `AddValidatorsFromAssemblyContaining` + `AddFluentValidationAutoValidation` |
