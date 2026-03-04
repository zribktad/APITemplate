# How to Create a REST Endpoint

This guide walks through the full workflow for adding a new versioned REST endpoint to the API. The example adds an `Orders` resource, following the same patterns used by `Products`, `Categories`, and `ProductReviews`.

---

## Overview

The REST layer follows **Clean Architecture**:

```
HTTP Request
  → Controller (Api/Controllers/V1/)     ← thin, no business logic
  → Service    (Application/Features/<Feature>/Services/)   ← business rules, maps DTOs ↔ entities
  → Repository (Infrastructure/Repositories/) ← data access
  → Database   (PostgreSQL via EF Core)
```

---

## Step 1 – Define the Domain Entity

Create the entity in `src/APITemplate/Domain/Entities/`:

```csharp
// Domain/Entities/Order.cs
namespace APITemplate.Domain.Entities;

public sealed class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
}
```

---

## Step 2 – Create the DTOs

DTOs decouple the API contract from the domain model. Place them in `src/APITemplate/Application/Features/<Feature>/DTOs/`.

**Request DTO** (what the client sends):

```csharp
// Application/Features/<Feature>/DTOs/CreateOrderRequest.cs
namespace APITemplate.Application.DTOs;

public sealed record CreateOrderRequest(
    Guid CustomerId,
    decimal TotalAmount
);
```

**Response DTO** (what the API returns):

```csharp
// Application/Features/<Feature>/DTOs/OrderResponse.cs
namespace APITemplate.Application.DTOs;

public sealed record OrderResponse(
    Guid Id,
    Guid CustomerId,
    decimal TotalAmount,
    DateTime CreatedAt
);
```

---

## Step 3 – Add the FluentValidation Validator

Validators live in `src/APITemplate/Application/Features/<Feature>/Validation/`. They are auto-discovered and invoked by `FluentValidation.AspNetCore` before the controller action runs.

```csharp
// Application/Features/<Feature>/Validation/CreateOrderRequestValidator.cs
using FluentValidation;

namespace APITemplate.Application.Validators;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("CustomerId is required.");

        RuleFor(x => x.TotalAmount)
            .GreaterThan(0).WithMessage("TotalAmount must be greater than zero.");
    }
}
```

FluentValidation returns HTTP 400 with a structured error body automatically when validation fails — no additional controller code needed.

---

## Step 4 – Define the Mapping Extension

Mappings keep the service layer clean. Place them in `src/APITemplate/Application/Features/<Feature>/Mappings/`:

```csharp
// Application/Features/<Feature>/Mappings/OrderMappings.cs
using APITemplate.Domain.Entities;

namespace APITemplate.Application.Mappings;

public static class OrderMappings
{
    public static OrderResponse ToResponse(this Order order)
        => new(order.Id, order.CustomerId, order.TotalAmount, order.CreatedAt);
}
```

---

## Step 5 – Define the Service Interface and Implementation

**Interface** in `Application/Features/<Feature>/Interfaces/`:

```csharp
// Application/Features/<Feature>/Interfaces/IOrderService.cs
using APITemplate.Application.DTOs;

namespace APITemplate.Application.Interfaces;

public interface IOrderService
{
    Task<OrderResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResponse<OrderResponse>> GetAllAsync(PaginationFilter filter, CancellationToken ct = default);
    Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

**Implementation** in `Application/Features/<Feature>/Services/`:

```csharp
// Application/Features/<Feature>/Services/OrderService.cs
using APITemplate.Application.DTOs;
using APITemplate.Application.Interfaces;
using APITemplate.Application.Mappings;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Services;

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public OrderService(IOrderRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrderResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var order = await _repository.GetByIdAsync(id, ct);
        return order?.ToResponse();
    }

    public async Task<PagedResponse<OrderResponse>> GetAllAsync(
        PaginationFilter filter, CancellationToken ct = default)
    {
        var items = await _repository.ListAsync(ct);
        var total = await _repository.CountAsync(ct);

        var skip = (filter.PageNumber - 1) * filter.PageSize;
        var pagedItems = items
            .Skip(skip)
            .Take(filter.PageSize)
            .Select(o => o.ToResponse())
            .ToList();

        return new PagedResponse<OrderResponse>(pagedItems, total,
            filter.PageNumber, filter.PageSize);
    }

    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        var order = new Order
        {
            Id          = Guid.NewGuid(),
            CustomerId  = request.CustomerId,
            TotalAmount = request.TotalAmount,
            CreatedAt   = DateTime.UtcNow
        };

        await _repository.AddAsync(order, ct);
        await _unitOfWork.CommitAsync(ct);
        return order.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct);
        await _unitOfWork.CommitAsync(ct);
    }
}
```

---

## Step 6 – Create the Repository

**Interface** in `Domain/Interfaces/`:

```csharp
// Domain/Interfaces/IOrderRepository.cs
using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

public interface IOrderRepository : IRepository<Order> { }
```

**Implementation** in `Infrastructure/Repositories/`:

```csharp
// Infrastructure/Repositories/OrderRepository.cs
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;

namespace APITemplate.Infrastructure.Repositories;

public sealed class OrderRepository : RepositoryBase<Order>, IOrderRepository
{
    public OrderRepository(AppDbContext dbContext) : base(dbContext) { }
}
```

---

## Step 7 – Add the Controller

Controllers live in `src/APITemplate/Api/Controllers/V1/`. They are thin — no business logic, only HTTP mapping:

```csharp
// Api/Controllers/V1/OrdersController.cs
using APITemplate.Application.DTOs;
using APITemplate.Application.Interfaces;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<OrderResponse>>> GetAll(
        [FromQuery] PaginationFilter filter, CancellationToken ct)
    {
        var orders = await _orderService.GetAllAsync(filter, ct);
        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id, CancellationToken ct)
    {
        var order = await _orderService.GetByIdAsync(id, ct);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(
        CreateOrderRequest request, CancellationToken ct)
    {
        var order = await _orderService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id, version = "1.0" }, order);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _orderService.DeleteAsync(id, ct);
        return NoContent();
    }
}
```

---

## Step 8 – Register in Dependency Injection

Open `src/APITemplate/Extensions/ServiceCollectionExtensions.cs`:

```csharp
// In AddPersistence():
services.AddScoped<IOrderRepository, OrderRepository>();

// In AddApplicationServices():
services.AddScoped<IOrderService, OrderService>();
```

Validators are discovered automatically from the assembly — no explicit registration needed.

---

## Step 9 – Create the EF Core Migration

After adding the `DbSet<Order>` to `AppDbContext` and the entity configuration:

```bash
dotnet ef migrations add AddOrder --project src/APITemplate --output-dir Migrations
dotnet ef database update --project src/APITemplate
```

See [ef-migration.md](ef-migration.md) for the full migration workflow.

---

## HTTP Endpoints Summary

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/v1/Orders` | ✅ Bearer | Paginated list |
| `GET` | `/api/v1/Orders/{id}` | ✅ Bearer | Single item |
| `POST` | `/api/v1/Orders` | ✅ Bearer | Create |
| `DELETE` | `/api/v1/Orders/{id}` | ✅ Bearer | Delete |

To obtain a Bearer token, see [authentication.md](authentication.md).

---

## Checklist

- [ ] Domain entity in `Domain/Entities/`
- [ ] Request + Response DTOs in `Application/Features/<Feature>/DTOs/`
- [ ] FluentValidation validator in `Application/Features/<Feature>/Validation/`
- [ ] Mapping extension in `Application/Features/<Feature>/Mappings/`
- [ ] Service interface + implementation in `Application/Features/<Feature>/`
- [ ] Repository interface + implementation
- [ ] Controller in `Api/Controllers/V1/`
- [ ] DI registration in `ServiceCollectionExtensions.cs`
- [ ] EF Core migration (see [ef-migration.md](ef-migration.md))

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Api/Controllers/V1/` | HTTP endpoint definitions |
| `Application/Features/<Feature>/DTOs/` | Request/response contracts |
| `Application/Features/<Feature>/Validation/` | Input validation rules |
| `Application/Features/<Feature>/Mappings/` | Entity ↔ DTO converters |
| `Application/Features/<Feature>/Services/` | Business logic |
| `Application/Features/<Feature>/Interfaces/` | Service contracts |
| `Domain/Entities/` | Domain models |
| `Domain/Interfaces/` | Repository contracts |
| `Infrastructure/Repositories/` | EF Core repository implementations |
| `Extensions/ServiceCollectionExtensions.cs` | DI registration |

