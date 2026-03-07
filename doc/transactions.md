# How to Use Transactions

This guide explains the relational write rules used in this project and how to wrap multiple database operations in a single atomic transaction using the **Unit of Work** pattern.

---

## Overview

The project provides two transaction mechanisms:

| Mechanism | When to use |
|-----------|------------|
| **Service write wrapper via `ExecuteTransactionalWriteAsync(...)`** | Default relational write shape used by application services in this template. |
| **Explicit transaction via `IUnitOfWork.ExecuteInTransactionAsync`** | Underlying transaction mechanism used by the service write wrapper. |

Both mechanisms are available through `IUnitOfWork`, which is registered as a scoped dependency and wraps `AppDbContext` (PostgreSQL/EF Core).

For relational persistence, the service layer owns commit orchestration:

- Repositories only stage changes in the EF Core change tracker.
- Application services call `ExecuteTransactionalWriteAsync(...)` for relational writes.
- `ExecuteTransactionalWriteAsync(...)` delegates to `IUnitOfWork.ExecuteInTransactionAsync(...)`.
- `IUnitOfWork.CommitAsync()` remains the low-level commit primitive used by `UnitOfWork` itself.
- Command-side validation lookups can still happen before the wrapper when that keeps the command flow clearer.
- Transient PostgreSQL retry behavior is configured through `Persistence:PostgresRetry`.
- Explicit transaction flows run inside EF Core's execution strategy so the whole transaction delegate can be replayed on transient provider failures.

> **MongoDB note:** MongoDB multi-document ACID transactions require a replica set or sharded cluster. See the [MongoDB transaction section](#mongodb-transactions) at the end of this guide.

---

## Service Write Wrapper

The default relational write pattern in this template is the application helper `ExecuteTransactionalWriteAsync(...)`.

```csharp
public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
{
    var product = await _unitOfWork.ExecuteTransactionalWriteAsync(async () =>
    {
        var entity = new Product
        {
            Id          = Guid.NewGuid(),
            Name        = request.Name,
            Description = request.Description,
            Price       = request.Price,
            CategoryId  = request.CategoryId,
            CreatedAt   = DateTime.UtcNow
        };

        await _repository.AddAsync(entity, ct); // stages the entity in the EF change tracker
        return entity;
    }, ct);

    return product.ToResponse();
}
```

This helper keeps all relational writes on one entry point, even when the underlying write is only a single staged repository operation.

For simple `CommitAsync()` paths, retry behavior comes from the Npgsql provider configuration. Business exceptions, validation failures, and authorization failures are not retried.

---

## Explicit Transaction (Multiple Entities)

`ExecuteTransactionalWriteAsync(...)` is backed by `ExecuteInTransactionAsync(...)`. When you need to understand or change the transaction boundary itself, this is the underlying mechanism:

The current shipped features use the higher-level helper, so treat the example below as the lower-level reference for future transaction-boundary work.

```csharp
public async Task TransferCategoryAsync(
    Guid productId,
    Guid targetCategoryId,
    CancellationToken ct = default)
{
    await _unitOfWork.ExecuteInTransactionAsync(async () =>
    {
        // 1. Load and update the product
        var product = await _productRepository.GetByIdAsync(productId, ct)
            ?? throw new NotFoundException(nameof(Product), productId);

        product.CategoryId = targetCategoryId;
        await _productRepository.UpdateAsync(product, ct);

        // 2. Increment product count on the target category
        var category = await _categoryRepository.GetByIdAsync(targetCategoryId, ct)
            ?? throw new NotFoundException(nameof(Category), targetCategoryId);

        category.ProductCount += 1;
        await _categoryRepository.UpdateAsync(category, ct);

    }, ct);
}
```

If any statement inside the lambda throws, `ExecuteInTransactionAsync` calls `RollbackAsync` and re-throws the exception — the database is left unchanged. Do not call `CommitAsync` inside the transaction lambda; the wrapper saves and commits after the delegate completes successfully.

When PostgreSQL retry is enabled, `UnitOfWork` executes the transaction block through EF Core's execution strategy. That allows transient provider/database failures to replay the full transaction delegate instead of retrying only the final save.

### How `ExecuteInTransactionAsync` Works

```csharp
// Infrastructure/Persistence/UnitOfWork.cs
public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
{
    await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
    try
    {
        await action();
        await _dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
    catch
    {
        await transaction.RollbackAsync(ct);
        throw;
    }
}
```

---

## Using Transactions in a Service

### 1. Inject `IUnitOfWork` in the service constructor

```csharp
public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IUnitOfWork _unitOfWork;

    public OrderService(
        IOrderRepository orderRepo,
        IInventoryRepository inventoryRepo,
        IUnitOfWork unitOfWork)
    {
        _orderRepo      = orderRepo;
        _inventoryRepo  = inventoryRepo;
        _unitOfWork     = unitOfWork;
    }
```

### 2. Call `ExecuteInTransactionAsync` for the multi-step operation

```csharp
    public async Task<OrderResponse> PlaceOrderAsync(CreateOrderRequest request, CancellationToken ct)
    {
        OrderResponse? response = null;

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Decrease inventory
            var item = await _inventoryRepo.GetBySkuAsync(request.Sku, ct)
                ?? throw new NotFoundException("InventoryItem", request.Sku);

            if (item.Stock < request.Quantity)
                throw new ValidationException("Insufficient stock.");

            item.Stock -= request.Quantity;
            await _inventoryRepo.UpdateAsync(item, ct);

            // Create the order
            var order = new Order
            {
                Id          = Guid.NewGuid(),
                CustomerId  = request.CustomerId,
                Sku         = request.Sku,
                Quantity    = request.Quantity,
                TotalAmount = item.UnitPrice * request.Quantity,
                CreatedAt   = DateTime.UtcNow
            };

            await _orderRepo.AddAsync(order, ct);

            response = order.ToResponse();
        }, ct);

        return response!;
    }
}
```

The helper keeps single-write and multi-step write flows on the same service-level shape:

```csharp
public async Task<ProductReviewResponse> CreateAsync(CreateProductReviewRequest request, CancellationToken ct)
{
    var product = await _productRepository.GetByIdAsync(request.ProductId, ct)
        ?? throw new NotFoundException("Product", request.ProductId);

    var review = await _unitOfWork.ExecuteTransactionalWriteAsync(async () =>
    {
        var entity = new ProductReview
        {
            ProductId = product.Id,
            Rating = request.Rating
        };

        await _reviewRepository.AddAsync(entity, ct);
        return entity;
    }, ct);

    return review.ToResponse();
}
```

---

## Registering the Service

Register both the new service and its dependencies in `ServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddPersistence(
    this IServiceCollection services, IConfiguration configuration)
{
    // ... existing registrations ...
    services.AddScoped<IOrderRepository, OrderRepository>();
    services.AddScoped<IInventoryRepository, InventoryRepository>();
    services.AddScoped<IUnitOfWork, UnitOfWork>();  // already registered once
    return services;
}

public static IServiceCollection AddApplicationServices(this IServiceCollection services)
{
    // ... existing registrations ...
    services.AddScoped<IOrderService, OrderService>();
    return services;
}
```

> `IUnitOfWork` is scoped, so all repositories and the UnitOfWork instance within a single HTTP request share the same `AppDbContext` — this is what makes the transaction span multiple repositories correctly.

---

## MongoDB Transactions

MongoDB supports multi-document ACID transactions on replica sets (version 4.0+) and sharded clusters (version 4.2+). The single-node setup in `docker-compose.yml` does **not** support transactions by default.

To use transactions in MongoDB, inject `MongoDbContext` and start a session:

```csharp
public async Task CreateOrderWithDataAsync(
    CreateOrderRequest order,
    CreateImageProductDataRequest media,
    CancellationToken ct)
{
    using var session = await _mongoDb.Client.StartSessionAsync(cancellationToken: ct);
    session.StartTransaction();

    try
    {
        var ordersCollection = _mongoDb.Database.GetCollection<Order>("orders");
        var mediaCollection  = _mongoDb.Database.GetCollection<ProductData>("product_data");

        await ordersCollection.InsertOneAsync(session, order.ToEntity(), cancellationToken: ct);
        await mediaCollection.InsertOneAsync(session, media.ToEntity(), cancellationToken: ct);

        await session.CommitTransactionAsync(ct);
    }
    catch
    {
        await session.AbortTransactionAsync(ct);
        throw;
    }
}
```

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Domain/Interfaces/IUnitOfWork.cs` | Transaction contract |
| `Infrastructure/Persistence/UnitOfWork.cs` | EF Core implementation |
| `Infrastructure/Persistence/AppDbContext.cs` | Shared EF Core context (scoped lifetime) |
| `Infrastructure/Persistence/MongoDbContext.cs` | MongoDB client wrapper |
| `Extensions/ServiceCollectionExtensions.cs` | `services.AddScoped<IUnitOfWork, UnitOfWork>()` |
