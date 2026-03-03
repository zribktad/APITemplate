# APITemplate

A scalable, clean, and modern template designed to jumpstart **.NET 10** Web API and Data-Driven applications. By providing a curated set of industry-standard libraries and combining modern **REST** APIs side-by-side with a robust **GraphQL** backend, it bridges the gap between typical monolithic development speed and Clean Architecture principles within a single maintainable repository.

## 📚 How-To Guides

Step-by-step guides for the most common workflows in this project:

| Guide | Description |
|-------|-------------|
| [GraphQL Endpoint](doc/graphql-endpoint.md) | Add a type, query, mutation, and optional DataLoader |
| [REST Endpoint](doc/rest-endpoint.md) | Full workflow: entity → DTO → validator → service → controller |
| [EF Core Migration](doc/ef-migration.md) | Create and apply PostgreSQL schema migrations |
| [MongoDB Migration](doc/mongodb-migration.md) | Create index and data migrations with Kot.MongoDB.Migrations |
| [Transactions](doc/transactions.md) | Wrap multiple operations in an atomic Unit of Work transaction |
| [Authentication](doc/authentication.md) | JWT login flow, protecting endpoints, and production guidance |
| [Stored Procedures](doc/stored-procedures.md) | Add a PostgreSQL function and call it safely from C# |
| [MongoDB Polymorphism](doc/mongodb-polymorphism.md) | Store multiple document subtypes in one collection |
| [Validation](doc/validation.md) | Add FluentValidation rules, cross-field rules, and shared validators |
| [Specifications](doc/specifications.md) | Write reusable EF Core query specifications with Ardalis |
| [Scalar & GraphQL UI](doc/scalar-and-graphql-ui.md) | Use the Scalar REST explorer and Nitro GraphQL playground |
| [Testing](doc/testing.md) | Write unit tests (services, validators, repositories) and integration tests |

---

## 🚀 Key Features

*   **Architecture Pattern:** Clean mapping of concerns inside a monolithic solution (emulating Clean Architecture). `Domain` rules and interfaces are isolated from `Application` logic and `Infrastructure`.
*   **Dual API Modalities:**
    *   **REST API:** Clean HTTP endpoints using versioned controllers (`Asp.Versioning.Mvc`).
    *   **GraphQL API:** Complex query batching via `HotChocolate`, integrated Mutations and DataLoaders to eliminate the N+1 problem.
*   **Modern Interactive Documentation:** Native `.NET 10` OpenAPI integrations displayed smoothly in the browser using **Scalar** `/scalar`. Includes **Nitro UI** `/graphql/ui` for testing queries natively.
*   **Dual Database Architecture:**
    *   **PostgreSQL + EF Core 10:** Relational entities (Products, Categories, Reviews) with the Repository + Unit of Work pattern.
    *   **MongoDB:** Semi-structured media metadata (ProductData) with a polymorphic document model and BSON discriminators.
*   **Domain Filtering:** Seamless filtering, sorting, and paging powered by `Ardalis.Specification` to decouple query models from infrastructural EF abstractions.
*   **Enterprise-Grade Utilities:**
    *   **Validation:** Pipelined model validation using `FluentValidation.AspNetCore`.
    *   **Cross-Cutting Concerns:** Unified configuration via `Serilog` (Logging) and fully centralized Global Exception Management (`GlobalExceptionHandlerMiddleware`).
    *   **Authentication:** Pre-configured JWT secure endpoint access.
    *   **Observability:** Health Checks (`/health`) natively tracking both PostgreSQL and MongoDB state.
*   **Robust Testing Engine:** Provides isolated internal `Integration` tests using test containers or `UseInMemoryDatabase` combined flawlessly with WebApplicationFactory.

---

## 🏗 Architecture Diagram

The application leverages a single `.csproj` separated rationally via namespaces that conform to typical clean layer boundaries. The goal is friction-free deployments and dependency chains while ensuring long-term code organization.

```mermaid
graph TD
    subgraph APITemplate [APITemplate Web API]
        direction TB

        subgraph PresentationLayer [API Layer]
            REST[Controllers V1]
            GQL[GraphQL Queries & Mutations]
            UI[Scalar / Nitro UI]
            MID[Middleware & Logging]
        end

        subgraph ApplicationLayer [Application Layer]
            Services[Business Services]
            DTO[Data Transfer Objects]
            Validators[Fluent Validation]
            Spec[Ardalis Specifications]
        end

        subgraph DomainLayer [Domain Layer]
            Entities[Entities & Aggregate Roots]
            Ex[Domain Exceptions]
            Irepo[Abstract Interfaces]
        end

        subgraph InfrastructureLayer [Infrastructure Layer]
            Repo[Concrete Repositories]
            UoW[Unit of Work]
            EF[EF Core AppDbContext]
            Mongo[MongoDbContext]
        end

        %% Linkages representing Dependencies
        REST --> MID
        GQL --> MID
        REST --> Services
        GQL --> Services
        GQL -.-> DataLoaders[DataLoaders]
        DataLoaders --> Services

        Services --> Irepo
        Services --> Spec
        Services -.-> DTO
        Services -.-> Validators

        Repo -.-> Irepo
        Repo --> EF
        Repo --> Mongo
        UoW -.-> Irepo
        Irepo -.-> Entities
        EF -.-> Entities
        Mongo -.-> Entities

        PresentationLayer --> ApplicationLayer
        ApplicationLayer --> DomainLayer
        InfrastructureLayer --> DomainLayer
    end

    DB[(PostgreSQL)]
    MDB[(MongoDB)]
    EF ---> DB
    Mongo ---> MDB
```

---

## 📦 Domain Class Diagram

This class diagram models the aggregate roots and entities located natively within `Domain/Entities/`.

```mermaid
classDiagram
    class Product {
        +Guid Id
        +string Name
        +string Description
        +decimal Price
        +DateTime CreatedAt
        +ICollection~ProductReview~ Reviews
    }

    class ProductReview {
        +Guid Id
        +Guid ProductId
        +string ReviewerName
        +string Comment
        +int Rating
        +DateTime CreatedAt
        +Product Product
    }

    class ProductData {
        <<abstract>>
        +string Id
        +string Title
        +string? Description
        +DateTime CreatedAt
    }

    class ImageProductData {
        +int Width
        +int Height
        +string Format
        +long FileSizeBytes
    }

    class VideoProductData {
        +int DurationSeconds
        +string Resolution
        +string Format
        +long FileSizeBytes
    }

    Product "1" *-- "0..*" ProductReview : owns
    ProductData <|-- ImageProductData : discriminator image
    ProductData <|-- VideoProductData : discriminator video
```

---

## 🛠 Technology Stack

*   **Runtime:** `.NET 10.0` Web SDK
*   **Relational Database:** PostgreSQL (`Npgsql`)
*   **Document Database:** MongoDB (`MongoDB.Driver`)
*   **ORM:** Entity Framework Core (`Microsoft.EntityFrameworkCore.Design`, `10.0`)
*   **API Toolkit:** ASP.NET Core, Asp.Versioning, `Scalar.AspNetCore`
*   **GraphQL Core:** HotChocolate `15.1`
*   **Utilities:** `Serilog.AspNetCore`, `FluentValidation`, `Ardalis.Specification`
*   **Test Suite:** xUnit, `Microsoft.AspNetCore.Mvc.Testing`

---

## 📂 Project Structure

This architecture deliberately leverages a single project (`APITemplate.csproj`) broken up securely by namespaces to mirror a traditional Clean Architecture without the multirepo/multiproject overhead:

```text
src/APITemplate/
├── Api/              # Presentation Tier (V1 REST Controllers, GraphQL Queries/Mutations, Global Middleware)
├── Application/      # Business Logic (Services, DTOs, FluentValidation, Ardalis Specs)
├── Domain/           # Core Logic (Entities, Value Objects, Domain Exceptions, Interfaces)
├── Infrastructure/   # Outer boundaries (AppDbContext, MongoDbContext, EF Core Repositories, MongoDB Repositories, Unit of Work)
└── Extensions/       # Startup IoC container bootstrappers
tests/APITemplate.Tests/
├── Integration/      # End-to-End API endpoint testing bridging a real/in-memory DB via WebApplicationFactory
└── Unit/             # Isolated internal service logic tests
```

---

## 🔐 Authentication & Examples

Most REST and GraphQL endpoints might be protected by JWT Authentication (`[Authorize]`). A sample HTTP file (`src/APITemplate/APITemplate.http`) is included for simple direct execution from VS Code or Visual Studio.

**1. Acquiring a JWT Token via REST:**
Send your configured `Auth:Username` and `Auth:Password` (default: `admin`/`admin` per Development settings) to:
```http
POST /api/v1/Auth/login
Content-Type: application/json

{
    "username": "admin",
    "password": "admin"
}
```

### ⚡ GraphQL DataLoaders (N+1 Problem Solved)
By leveraging HotChocolate's built-in **DataLoaders** pipeline (`ProductReviewsByProductDataLoader`), fetching deeply nested parent-child relationships avoids querying the database `n` times. The framework collects IDs requested entirely within the GraphQL query, then queries the underlying EF Core PostgreSQL implementation precisely *once*.

**2. Example GraphQL Query (Using the token via `Authorization: Bearer <token>`):**
```graphql
query {
  products(take: 10, skip: 0) {
    items {
      id
      name
      price
      # Below triggers ONE bulk DataLoader fetch under the hood!
      reviews {
        reviewerName
        rating
      }
    }
    totalCount
  }
}
```

**3. Example GraphQL Mutation:**
```graphql
mutation {
  createProduct(input: {
    name: "New Masterpiece Board Game"
    price: 49.99
    description: "An epic adventure game"
  }) {
    id
    name
  }
}
```

---

## 🗄 Stored Procedure Pattern (EF Core + PostgreSQL)

EF Core's `FromSql()` lets you call stored procedures while still getting full object materialisation and parameterised queries. The pattern below is used for the `GET /api/v1/categories/{id}/stats` endpoint.

### When to use a stored procedure

| Situation | Use LINQ | Use Stored Procedure |
|-----------|----------|----------------------|
| Simple CRUD filtering / paging | ✅ | |
| Complex multi-table aggregations | | ✅ |
| Reusable DB-side business logic | | ✅ |
| Query needs full EF change tracking | ✅ | |

### 4-step implementation

**Step 1 — Keyless entity** (no backing table, only a result-set shape)

```csharp
// Domain/Entities/ProductCategoryStats.cs
public sealed class ProductCategoryStats
{
    public Guid   CategoryId    { get; set; }
    public string CategoryName  { get; set; } = string.Empty;
    public long   ProductCount  { get; set; }
    public decimal AveragePrice { get; set; }
    public long   TotalReviews  { get; set; }
}
```

**Step 2 — EF configuration** (`HasNoKey` + `ExcludeFromMigrations`)

```csharp
// Infrastructure/Persistence/Configurations/ProductCategoryStatsConfiguration.cs
public sealed class ProductCategoryStatsConfiguration : IEntityTypeConfiguration<ProductCategoryStats>
{
    public void Configure(EntityTypeBuilder<ProductCategoryStats> builder)
    {
        builder.HasNoKey();
        // No backing table — skip this type during 'dotnet ef migrations add'
        builder.ToTable("ProductCategoryStats", t => t.ExcludeFromMigrations());
    }
}
```

**Step 3 — Migration** (create the PostgreSQL function in `Up`, drop it in `Down`)

```csharp
migrationBuilder.Sql("""
    CREATE OR REPLACE FUNCTION get_product_category_stats(p_category_id UUID)
    RETURNS TABLE(
        category_id UUID, category_name TEXT,
        product_count BIGINT, average_price NUMERIC, total_reviews BIGINT
    )
    LANGUAGE plpgsql AS $$
    BEGIN
        RETURN QUERY
        SELECT c."Id", c."Name"::TEXT,
               COUNT(DISTINCT p."Id"),
               COALESCE(AVG(p."Price"), 0),
               COUNT(pr."Id")
        FROM "Categories" c
        LEFT JOIN "Products"       p  ON p."CategoryId" = c."Id"
        LEFT JOIN "ProductReviews" pr ON pr."ProductId"  = p."Id"
        WHERE c."Id" = p_category_id
        GROUP BY c."Id", c."Name";
    END;
    $$;
    """);

// Down:
migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_product_category_stats(UUID);");
```

**Step 4 — Repository call** via `FromSql` (auto-parameterised, injection-safe)

```csharp
// Infrastructure/Repositories/CategoryRepository.cs
public Task<ProductCategoryStats?> GetStatsByIdAsync(Guid categoryId, CancellationToken ct = default)
{
    // The interpolated {categoryId} is converted to a @p0 parameter by EF Core —
    // never use string concatenation here.
    return AppDb.ProductCategoryStats
        .FromSql($"SELECT * FROM get_product_category_stats({categoryId})")
        .FirstOrDefaultAsync(ct);
}
```

### Full request flow

```
GET /api/v1/categories/{id}/stats
        │
        ▼
CategoriesController.GetStats()
        │
        ▼
CategoryService.GetStatsAsync()
        │
        ▼
CategoryRepository.GetStatsByIdAsync()
        │  FromSql($"SELECT * FROM get_product_category_stats({id})")
        ▼
PostgreSQL  →  get_product_category_stats(p_category_id)
        │  returns: category_id, category_name, product_count, average_price, total_reviews
        ▼
EF Core maps columns → ProductCategoryStats (keyless entity)
        │
        ▼
ProductCategoryStatsResponse  (DTO returned to client)
```

---

## 🍃 MongoDB Polymorphic Pattern (ProductData)

The `ProductData` feature demonstrates a **polymorphic document model** in MongoDB, where a single collection stores two distinct subtypes (`ImageProductData`, `VideoProductData`) using the BSON discriminator pattern.

### When to use MongoDB vs PostgreSQL

| Situation | Use PostgreSQL | Use MongoDB |
|-----------|---------------|-------------|
| Relational data with foreign keys | ✅ | |
| Fixed, well-defined schema | ✅ | |
| ACID transactions across tables | ✅ | |
| Semi-structured or evolving schemas | | ✅ |
| Polymorphic document hierarchies | | ✅ |
| Media metadata, logs, events | | ✅ |

### Discriminator-based inheritance

```csharp
// Domain/Entities/ProductData.cs
[BsonDiscriminator(RootClass = true)]
[BsonKnownTypes(typeof(ImageProductData), typeof(VideoProductData))]
public abstract class ProductData
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; init; } = ObjectId.GenerateNewId().ToString();
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

// Domain/Entities/ImageProductData.cs
[BsonDiscriminator("image")]
public sealed class ImageProductData : ProductData
{
    public int Width { get; init; }
    public int Height { get; init; }
    public string Format { get; init; } = string.Empty;   // jpg | png | gif | webp
    public long FileSizeBytes { get; init; }
}

// Domain/Entities/VideoProductData.cs
[BsonDiscriminator("video")]
public sealed class VideoProductData : ProductData
{
    public int DurationSeconds { get; init; }
    public string Resolution { get; init; } = string.Empty; // 720p | 1080p | 4K
    public string Format { get; init; } = string.Empty;     // mp4 | avi | mkv
    public long FileSizeBytes { get; init; }
}
```

MongoDB stores a `_t` discriminator field automatically, enabling polymorphic queries against the single `product_data` collection.

### REST endpoints

Base route: `api/v{version}/product-data` — all endpoints require JWT authorization.

| Method | Endpoint | Request | Response | Purpose |
|--------|----------|---------|----------|---------|
| `GET` | `/` | Query: `type` (optional) | `List<ProductDataResponse>` | List all or filter by type |
| `GET` | `/{id}` | MongoDB ObjectId string | `ProductDataResponse` / 404 | Get by ID |
| `POST` | `/image` | `CreateImageProductDataRequest` | `ProductDataResponse` 201 | Create image metadata |
| `POST` | `/video` | `CreateVideoProductDataRequest` | `ProductDataResponse` 201 | Create video metadata |
| `DELETE` | `/{id}` | MongoDB ObjectId string | 204 No Content | Delete by ID |

### Configuration

```json
// appsettings.json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "apitemplate"
  }
}
```

### Service registration

```csharp
// Extensions/ServiceCollectionExtensions.cs — AddMongoDB()
services.Configure<MongoDbSettings>(configuration.GetSection("MongoDB"));
services.AddSingleton<MongoDbContext>();
services.AddScoped<IProductDataRepository, ProductDataRepository>();
services.AddScoped<IProductDataService, ProductDataService>();
services.AddHealthChecks().AddMongoDb(...);
```

### Full request flow

```
POST /api/v1/product-data/image
        │
        ▼
ProductDataController.CreateImage()
        │  FluentValidation auto-validates CreateImageProductDataRequest
        ▼
ProductDataService.CreateImageAsync()
        │  Maps request → ImageProductData entity
        ▼
ProductDataRepository.CreateAsync()
        │  InsertOneAsync into product_data collection
        ▼
MongoDB  →  stores { _t: "image", Title, Width, Height, Format, ... }
        │
        ▼
ProductDataMappings.ToResponse()  (switch expression, polymorphic)
        │
        ▼
ProductDataResponse  (Type, Id, Title, Width, Height, Format, ...)
```

---

## 🚀 CI/CD & Deployments

While not natively shipped via default configuration files, this structure allows simple portability across cloud ecosystems:

**GitHub Actions / Azure Pipelines Structure:**
1. **Restore:** `dotnet restore src/APITemplate.sln`
2. **Build:** `dotnet build --no-restore src/APITemplate.sln`
3. **Test:** `dotnet test --no-build src/APITemplate.sln`
4. **Publish Container:** `docker build -t apitemplate-image:1.0 -f src/APITemplate/Dockerfile .`
5. **Push Registry:** `docker push <registry>/apitemplate-image:1.0`

Because the application encompasses the database (natively via DI) and HTTP context fully self-contained using containerization, it scales efficiently behind Kubernetes Ingress (Nginx) or any App Service / Container Apps equivalent, maintaining state natively using PostgreSQL and MongoDB.

---

## 🧪 Testing

The repository maintains an inclusive combination of **Unit Tests** and **Integration Tests** executing over a seamless Test-Host infrastructure.

To run the whole test suite:
```bash
dotnet test
```

---

## 🏃 Getting Started

### Prerequisites
*   [.NET 10 SDK installed locally](https://dotnet.microsoft.com/)
*   [Docker Desktop](https://www.docker.com/) (Optional, convenient for running infrastructure).

### Quick Start (Using Docker Compose)

The template consists of a ready-to-use Docker environment to spool up both the PostgreSQL and MongoDB containers alongside the built API application immediately:

```bash
# Start up databases along with the API container
docker-compose up -d --build
```
> The API will bind natively to `http://localhost:8080`.

### Running Locally without Containerization

If you prefer spinning the `.NET Web API` application bare-metal, guarantee that reachable PostgreSQL and MongoDB instances are available. Apply your connection strings in `src/APITemplate/appsettings.Development.json`.

1. Run EF Migrations to build the default database tables:
    ```bash
    dotnet ef database update --project src/APITemplate
    ```
2. Spawn the Web Application:
    ```bash
    dotnet run --project src/APITemplate
    ```

### Available Endpoints & User Interfaces

Once fully spun up under a Development environment, check out:
- **Interactive REST API Documentation (Scalar):** `http://localhost:<port>/scalar`
- **Native GraphQL IDE (Nitro UI):** `http://localhost:<port>/graphql/ui`
- **Environment & Database Health Check:** `http://localhost:<port>/health`
