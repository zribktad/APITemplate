# How to Create a New API Endpoint

Step-by-step guide for adding a new REST API endpoint to this project. Uses **Product** as the reference implementation.

---

## Architecture Overview

```
HTTP Request
  → Controller (Api)
    → Service (Application)
      → Repository (Infrastructure)
        → Database (PostgreSQL)
```

Layers follow **Clean Architecture** — dependencies point inward:

```
Domain  ←  Application  ←  Infrastructure  ←  Api
```

---

## File Structure per Feature

```
Application/Features/{Feature}/
├── DTOs/
│   ├── Create{Feature}Request.cs     # Input DTO (create)
│   ├── Update{Feature}Request.cs     # Input DTO (update)
│   ├── {Feature}Response.cs          # Output DTO
│   └── {Feature}Filter.cs            # Query/filter parameters
├── Interfaces/
│   ├── I{Feature}Service.cs          # Write operations contract
│   └── I{Feature}QueryService.cs     # Read operations contract
├── Services/
│   ├── {Feature}Service.cs           # Write operations
│   └── {Feature}QueryService.cs      # Read operations
├── Specifications/
│   ├── {Feature}Specification.cs     # Filtered/sorted/paged query
│   ├── {Feature}CountSpecification.cs # Count query (for pagination)
│   └── {Feature}FilterCriteria.cs    # Reusable filter logic
├── Validation/
│   ├── Create{Feature}RequestValidator.cs
│   ├── Update{Feature}RequestValidator.cs
│   └── {Feature}FilterValidator.cs
├── Mappings/
│   └── {Feature}Mappings.cs          # Entity → Response mapping
└── {Feature}SortFields.cs            # Sort field definitions
```

Additional files outside the feature folder:

```
Domain/Entities/{Feature}.cs                              # Domain entity
Domain/Interfaces/I{Feature}Repository.cs                 # Repository contract
Infrastructure/Repositories/{Feature}Repository.cs        # Repository implementation
Infrastructure/Persistence/Configurations/{Feature}Configuration.cs  # EF Core config
Api/Controllers/V1/{Feature}sController.cs                # REST controller
Application/Common/Errors/ErrorCatalog.cs                 # Error codes (add section)
Extensions/ServiceCollectionExtensions.cs                 # DI registration
```

---

## Step-by-Step Guide

### 1. Domain Entity

Create `Domain/Entities/{Feature}.cs`. Implement `IAuditableTenantEntity` for full multi-tenancy, auditing, soft-delete, and concurrency support.

```csharp
namespace APITemplate.Domain.Entities;

public sealed class Product : IAuditableTenantEntity
{
    public Guid Id { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }

    // Relationships
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    // IAuditableTenantEntity members (always include these)
    public Guid TenantId { get; set; }
    public AuditInfo Audit { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
```

> `IAuditableTenantEntity` = `ITenantEntity` + `IAuditableEntity` + `ISoftDeletable` + `IHasRowVersion`. The `AppDbContext` auto-handles tenancy, auditing, and soft-delete for any entity implementing this interface.

---

### 2. EF Core Configuration

Create `Infrastructure/Persistence/Configurations/{Feature}Configuration.cs`:

```csharp
using APITemplate.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APITemplate.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.ConfigureTenantAuditable(); // Sets up audit, tenant, soft-delete, row version

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        builder.Property(p => p.Price)
            .HasPrecision(18, 2);

        // Relationships
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(p => new { p.TenantId, p.Name });
    }
}
```

> Always call `builder.ConfigureTenantAuditable()` — it sets up audit fields, soft-delete, row version, tenant indexes, and a check constraint for soft-delete consistency.

Add a `DbSet` to `AppDbContext`:

```csharp
public DbSet<Product> Products => Set<Product>();
```

---

### 3. Repository

**Interface** — `Domain/Interfaces/I{Feature}Repository.cs`:

```csharp
using APITemplate.Domain.Entities;

namespace APITemplate.Domain.Interfaces;

public interface IProductRepository : IRepository<Product>
{
    // Add feature-specific query methods here if needed
}
```

> `IRepository<T>` inherits from Ardalis `IRepositoryBase<T>` and adds `DeleteAsync(Guid id)`.

**Implementation** — `Infrastructure/Repositories/{Feature}Repository.cs`:

```csharp
using APITemplate.Domain.Entities;
using APITemplate.Domain.Interfaces;
using APITemplate.Infrastructure.Persistence;

namespace APITemplate.Infrastructure.Repositories;

public sealed class ProductRepository : RepositoryBase<Product>, IProductRepository
{
    public ProductRepository(AppDbContext dbContext) : base(dbContext) { }
}
```

> `RepositoryBase<T>` overrides `AddAsync`/`UpdateAsync` to NOT call `SaveChangesAsync` — that's the `IUnitOfWork`'s job.

---

### 4. DTOs

**Request DTOs** — `Application/Features/{Feature}/DTOs/`:

```csharp
using System.ComponentModel.DataAnnotations;
using APITemplate.Application.Common.Validation;

namespace APITemplate.Application.Features.Product.DTOs;

public sealed record CreateProductRequest(
    [property: NotEmpty(ErrorMessage = "Product name is required.")]
    [property: MaxLength(200, ErrorMessage = "Product name must not exceed 200 characters.")]
    string Name,
    string? Description,
    [property: Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero.")]
    decimal Price,
    Guid? CategoryId = null) : IProductRequest;
```

> Use **Data Annotations** for simple per-field validation. Use **FluentValidation** for cross-field rules.

**Response DTO**:

```csharp
namespace APITemplate.Application.Features.Product.DTOs;

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    DateTime CreatedAtUtc);
```

**Filter DTO** (for GET all / list endpoints):

```csharp
using APITemplate.Application.Common.Contracts;

namespace APITemplate.Application.Features.Product.DTOs;

public sealed record ProductFilter(
    string? Name = null,
    string? Description = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    DateTime? CreatedFrom = null,
    DateTime? CreatedTo = null,
    string? SortBy = null,
    string? SortDirection = null,
    int PageNumber = 1,
    int PageSize = 10) : PaginationFilter(PageNumber, PageSize), IDateRangeFilter, ISortableFilter;
```

> Implement `ISortableFilter` for sorting, `IDateRangeFilter` for date range filtering. Extend `PaginationFilter` for page number/size validation.

---

### 5. Mappings

`Application/Features/{Feature}/Mappings/{Feature}Mappings.cs`:

```csharp
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Mappings;

public static class ProductMappings
{
    public static ProductResponse ToResponse(this ProductEntity product) =>
        new(product.Id, product.Name, product.Description, product.Price, product.Audit.CreatedAtUtc);
}
```

> Static extension methods — no mapping library needed. Simple and explicit.

---

### 6. Sort Fields

`Application/Features/{Feature}/{Feature}SortFields.cs`:

```csharp
using APITemplate.Application.Common.Sorting;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product;

public static class ProductSortFields
{
    public static readonly SortField Name = new("name");
    public static readonly SortField Price = new("price");
    public static readonly SortField CreatedAt = new("createdAt");

    public static readonly SortFieldMap<ProductEntity> Map = new SortFieldMap<ProductEntity>()
        .Add(Name, p => p.Name)
        .Add(Price, p => (object)p.Price)
        .Default(p => p.Audit.CreatedAtUtc);
}
```

> Single source of truth for sort field names and expressions. Used by both validators and specifications. To add a new sort field, just add one `.Add(...)` line.

---

### 7. Specifications

**Main query specification** — `Application/Features/{Feature}/Specifications/{Feature}Specification.cs`:

```csharp
using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;

public sealed class ProductSpecification : Specification<ProductEntity, ProductResponse>
{
    public ProductSpecification(ProductFilter filter)
    {
        // 1. Filter
        ProductFilterCriteria.Apply(Query, filter);

        // 2. Sort
        ProductSortFields.Map.ApplySort(Query, filter.SortBy, filter.SortDirection);

        // 3. Project to DTO
        Query.Select(p => new ProductResponse(
            p.Id, p.Name, p.Description, p.Price, p.Audit.CreatedAtUtc));

        // 4. Paginate
        Query.Skip((filter.PageNumber - 1) * filter.PageSize)
             .Take(filter.PageSize);
    }
}
```

**Count specification** (for total count in pagination):

```csharp
using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;

public sealed class ProductCountSpecification : Specification<ProductEntity>
{
    public ProductCountSpecification(ProductFilter filter)
    {
        ProductFilterCriteria.Apply(Query, filter);
    }
}
```

**Filter criteria** (reusable between spec and count spec):

```csharp
using Ardalis.Specification;
using ProductEntity = APITemplate.Domain.Entities.Product;

namespace APITemplate.Application.Features.Product.Specifications;

internal static class ProductFilterCriteria
{
    internal static void Apply(ISpecificationBuilder<ProductEntity> query, ProductFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Name))
            query.Where(p => p.Name.Contains(filter.Name));

        if (!string.IsNullOrWhiteSpace(filter.Description))
            query.Where(p => p.Description != null && p.Description.Contains(filter.Description));

        if (filter.MinPrice.HasValue)
            query.Where(p => p.Price >= filter.MinPrice.Value);

        if (filter.MaxPrice.HasValue)
            query.Where(p => p.Price <= filter.MaxPrice.Value);

        if (filter.CreatedFrom.HasValue)
            query.Where(p => p.Audit.CreatedAtUtc >= filter.CreatedFrom.Value);

        if (filter.CreatedTo.HasValue)
            query.Where(p => p.Audit.CreatedAtUtc <= filter.CreatedTo.Value);
    }
}
```

---

### 8. Validators

Use FluentValidation for cross-field rules. Data Annotations handle simple per-field validation.

**Shared request validator base** (for rules shared between Create and Update):

```csharp
using FluentValidation;

namespace APITemplate.Application.Features.Product.Validation;

public abstract class ProductRequestValidatorBase<T> : AbstractValidator<T>
    where T : IProductRequest
{
    protected ProductRequestValidatorBase()
    {
        // Cross-field rule: cannot be expressed via Data Annotations
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required for products priced above 1000.")
            .When(x => x.Price > 1000);
    }
}
```

**Create/Update validators** (inherit shared rules):

```csharp
namespace APITemplate.Application.Features.Product.Validation;

public sealed class CreateProductRequestValidator : ProductRequestValidatorBase<CreateProductRequest>;
public sealed class UpdateProductRequestValidator : ProductRequestValidatorBase<UpdateProductRequest>;
```

**Filter validator**:

```csharp
using APITemplate.Application.Common.Validation;
using FluentValidation;

namespace APITemplate.Application.Features.Product.Validation;

public sealed class ProductFilterValidator : AbstractValidator<ProductFilter>
{
    public ProductFilterValidator()
    {
        Include(new PaginationFilterValidator());
        Include(new DateRangeFilterValidator<ProductFilter>());
        Include(new SortableFilterValidator<ProductFilter>(ProductSortFields.Map.AllowedNames));

        // Feature-specific filter rules
        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).WithMessage("MinPrice must be >= 0.")
            .When(x => x.MinPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(0).WithMessage("MaxPrice must be >= 0.")
            .When(x => x.MaxPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(x => x.MinPrice!.Value)
            .WithMessage("MaxPrice must be >= MinPrice.")
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue);
    }
}
```

> Validators are auto-discovered and registered via `AddValidatorsFromAssemblyContaining<>()` in DI. No manual registration needed.

---

### 9. Service Layer

Services are split into **write** (commands) and **read** (queries).

**Interfaces** — `Application/Features/{Feature}/Interfaces/`:

```csharp
namespace APITemplate.Application.Features.Product.Interfaces;

public interface IProductService
{
    Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResponse<ProductResponse>> GetAllAsync(ProductFilter filter, CancellationToken ct = default);
    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IProductQueryService
{
    Task<PagedResponse<ProductResponse>> GetPagedAsync(ProductFilter filter, CancellationToken ct = default);
    Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
```

**Query service** — reads via Specifications:

```csharp
using APITemplate.Application.Features.Product.Mappings;
using APITemplate.Application.Features.Product.Specifications;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.Product.Services;

public sealed class ProductQueryService : IProductQueryService
{
    private readonly IProductRepository _repository;

    public ProductQueryService(IProductRepository repository) => _repository = repository;

    public async Task<PagedResponse<ProductResponse>> GetPagedAsync(
        ProductFilter filter, CancellationToken ct = default)
    {
        var items = await _repository.ListAsync(new ProductSpecification(filter), ct);
        var totalCount = await _repository.CountAsync(new ProductCountSpecification(filter), ct);
        return new PagedResponse<ProductResponse>(items, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _repository.GetByIdAsync(id, ct);
        return item?.ToResponse();
    }
}
```

**Write service** — orchestrates domain operations + UnitOfWork:

```csharp
using APITemplate.Application.Features.Product.Mappings;
using ProductEntity = APITemplate.Domain.Entities.Product;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.Product.Services;

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly IProductQueryService _queryService;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(
        IProductRepository repository,
        IProductQueryService queryService,
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _queryService = queryService;
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _queryService.GetByIdAsync(id, ct);

    public Task<PagedResponse<ProductResponse>> GetAllAsync(
        ProductFilter filter, CancellationToken ct = default)
        => _queryService.GetPagedAsync(filter, ct);

    public async Task<ProductResponse> CreateAsync(
        CreateProductRequest request, CancellationToken ct = default)
    {
        await ValidateCategoryExistsAsync(request.CategoryId, ct);

        var product = new ProductEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            CategoryId = request.CategoryId
        };

        await _repository.AddAsync(product, ct);
        await _unitOfWork.CommitAsync(ct);
        return product.ToResponse();
    }

    public async Task UpdateAsync(
        Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(
                nameof(ProductEntity), id, ErrorCatalog.Products.NotFound);

        await ValidateCategoryExistsAsync(request.CategoryId, ct);

        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.CategoryId = request.CategoryId;

        await _repository.UpdateAsync(product, ct);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repository.DeleteAsync(id, ct, ErrorCatalog.Products.NotFound);
        await _unitOfWork.CommitAsync(ct);
    }

    private async Task ValidateCategoryExistsAsync(Guid? categoryId, CancellationToken ct)
    {
        if (!categoryId.HasValue) return;

        _ = await _categoryRepository.GetByIdAsync(categoryId.Value, ct)
            ?? throw new NotFoundException(
                nameof(Domain.Entities.Category), categoryId.Value,
                ErrorCatalog.Categories.NotFound);
    }
}
```

> **Key pattern**: Repository tracks changes, `IUnitOfWork.CommitAsync()` persists them. Never call `SaveChangesAsync` in repositories.

---

### 10. Error Codes

Add a section to `Application/Common/Errors/ErrorCatalog.cs`:

```csharp
public static class Products
{
    public const string NotFound = "PRD-0404";
}
```

> Convention: `{PREFIX}-{HTTP_STATUS}`. Used in `NotFoundException` for structured error responses.

---

### 11. Controller

`Api/Controllers/V1/{Feature}sController.cs`:

```csharp
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APITemplate.Api.Controllers.V1;

[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
        => _productService = productService;

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ProductResponse>>> GetAll(
        [FromQuery] ProductFilter filter, CancellationToken ct)
    {
        var products = await _productService.GetAllAsync(filter, ct);
        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken ct)
    {
        var product = await _productService.GetByIdAsync(id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductRequest request, CancellationToken ct)
    {
        var product = await _productService.CreateAsync(request, ct);
        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id, version = "1.0" },
            product);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        await _productService.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _productService.DeleteAsync(id, ct);
        return NoContent();
    }
}
```

> Controllers depend only on service interfaces. Validation is automatic (FluentValidation auto-validation middleware). Exceptions are caught by `ApiExceptionHandler` and converted to RFC 7807 ProblemDetails.

---

### 12. Register in DI

In `Extensions/ServiceCollectionExtensions.cs`:

**`AddPersistence` method** — add repository:
```csharp
services.AddScoped<IProductRepository, ProductRepository>();
```

**`AddApplicationServices` method** — add services:
```csharp
services.AddScoped<IProductService, ProductService>();
services.AddScoped<IProductQueryService, ProductQueryService>();
```

> Validators are auto-registered via `AddValidatorsFromAssemblyContaining<>()` — no manual registration needed.

---

### 13. Create EF Migration

```bash
dotnet ef migrations add Add{Feature} --project src/APITemplate
```

---

## Checklist

- [ ] Domain entity implementing `IAuditableTenantEntity`
- [ ] EF Core configuration with `ConfigureTenantAuditable()`
- [ ] `DbSet` in `AppDbContext`
- [ ] Repository interface (`IRepository<T>`) and implementation (`RepositoryBase<T>`)
- [ ] DTOs: Create request, Update request, Response, Filter
- [ ] Mappings: Entity → Response extension method
- [ ] Sort fields: `SortFieldMap<T>` definition
- [ ] Specifications: main (filtered + sorted + paged + projected), count
- [ ] Filter criteria: reusable between main and count specs
- [ ] Validators: request validators, filter validator
- [ ] Service interface and implementation (write + query)
- [ ] Error codes in `ErrorCatalog`
- [ ] Controller with `[ApiVersion]`, `[Authorize]`, versioned route
- [ ] DI registration in `ServiceCollectionExtensions`
- [ ] EF migration

---

## Cross-Cutting Concerns (handled automatically)

| Concern | How | Where |
|---------|-----|-------|
| **Multi-tenancy** | Global query filter on `TenantId` | `AppDbContext` |
| **Soft delete** | `Remove()` → sets `IsDeleted = true` | `AppDbContext.SaveChangesAsync` |
| **Audit trail** | Auto-stamps `CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`, `UpdatedBy` | `AppDbContext.SaveChangesAsync` |
| **Concurrency** | Application-managed `RowVersion` | `AppDbContext.SaveChangesAsync` |
| **Validation** | Data Annotations + FluentValidation auto-validation | Middleware |
| **Error handling** | `AppException` hierarchy → RFC 7807 ProblemDetails | `ApiExceptionHandler` |
| **JWT auth** | `[Authorize]` + tenant claim validation | Middleware |

---

## Request Flow

```
HTTP Request
  → Exception Handler Middleware
    → Request Context Middleware (extracts tenant/actor from JWT)
      → Authentication Middleware (validates JWT)
        → Authorization Middleware (checks [Authorize])
          → FluentValidation Auto-Validation (validates DTOs)
            → Controller action
              → Service (business logic)
                → Repository (data access via Specification)
                  → AppDbContext (auditing, tenancy, soft-delete)
                    → PostgreSQL
```
