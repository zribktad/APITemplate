# How to Use Transactions

This guide explains how to wrap multiple database operations in a single atomic transaction using the **Unit of Work** pattern implemented in this project.

---

## Overview

The project provides two transaction mechanisms:

| Mechanism | When to use |
|-----------|------------|
| **Implicit (auto-commit)** | A single service operation — one entity, one save. Most common case. |
| **Explicit transaction via `IUnitOfWork.ExecuteInTransactionAsync`** | Multiple entities must succeed or fail together. |

Both mechanisms are available through `IUnitOfWork`, which is registered as a scoped dependency and wraps `AppDbContext` (PostgreSQL/EF Core).

> **MongoDB note:** MongoDB multi-document ACID transactions require a replica set or sharded cluster. See the [MongoDB transaction section](#mongodb-transactions) at the end of this guide.

---

## Implicit Commit (Single Entity)

The most common case: add or update one entity and commit.

```csharp
public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct = default)
{
    var product = new Product
    {
        Id          = Guid.NewGuid(),
        Name        = request.Name,
        Description = request.Description,
        Price       = request.Price,
        CategoryId  = request.CategoryId,
        CreatedAt   = DateTime.UtcNow
    };

    await _repository.AddAsync(product, ct);   // stages the entity in the EF change tracker
    await _unitOfWork.CommitAsync(ct);          // calls SaveChangesAsync → one INSERT
    return product.ToResponse();
}
```

`CommitAsync` calls `AppDbContext.SaveChangesAsync`. With our EF Core/PostgreSQL setup, EF Core will create and use a database transaction when needed (for example, when multiple statements are generated), but the exact implicit transaction behavior can vary by provider and configuration.

---

## Explicit Transaction (Multiple Entities)

When two or more entities must be persisted atomically — either both succeed or the database is left unchanged — use `ExecuteInTransactionAsync`:

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

        // 3. Commit both changes inside the same transaction
        await _unitOfWork.CommitAsync(ct);

    }, ct);
}
```

If any statement inside the lambda throws, `ExecuteInTransactionAsync` calls `RollbackAsync` and re-throws the exception — the database is left unchanged.

### How `ExecuteInTransactionAsync` Works

```csharp
// Infrastructure/Persistence/UnitOfWork.cs
public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
{
    await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
    try
    {
        await action();
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

            // Both changes saved in one atomic transaction
            await _unitOfWork.CommitAsync(ct);

            response = order.ToResponse();
        }, ct);

        return response!;
    }
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
