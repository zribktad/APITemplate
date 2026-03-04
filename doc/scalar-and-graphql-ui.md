# How to Use Scalar and the GraphQL UI

This guide explains how to interact with the API using the two built-in browser-based interfaces: **Scalar** (for REST) and **Nitro** (for GraphQL). Both are available automatically in the `Development` environment.

---

## Prerequisites

Start the application in Development mode:

```bash
# Option A – Docker Compose (recommended, starts PostgreSQL + MongoDB too)
docker-compose up

# Option B – Local run (requires PostgreSQL + MongoDB already running)
dotnet run --project src/APITemplate
```

Default base URL: `http://localhost:5174`

---

## Scalar – REST API Explorer

Scalar is a modern OpenAPI-based browser UI available at:

```
http://localhost:5174/scalar/v1
```

### What You Can Do in Scalar

| Action | Description |
|--------|-------------|
| Browse endpoints | All versioned REST routes grouped by controller tag |
| Read schema | Request/response types with field descriptions |
| Authenticate | Enter a JWT token once, all requests carry it |
| Send requests | Execute any endpoint directly in the browser |
| View examples | Generated example bodies for every operation |

### Step-by-Step: Authenticate in Scalar

1. Open `http://localhost:5174/scalar/v1`
2. Expand the **Auth → POST /api/v1/Auth/login** operation
3. Click **Send** with the default body:
   ```json
   { "username": "admin", "password": "admin" }
   ```
4. Copy the `accessToken` from the response
5. Click the **🔐 Authenticate** button (top-right)
6. Paste the token into the **Bearer** field → click **Save**

All subsequent requests in this session will include the `Authorization: Bearer <token>` header automatically.

### Raw OpenAPI Document

The raw OpenAPI 3.1 JSON document is available at:

```
http://localhost:5174/openapi/v1.json
```

This can be imported into Postman, Insomnia, or any OpenAPI-compatible tool.

### Configuration

Scalar is configured in `src/APITemplate/Extensions/ApplicationBuilderExtensions.cs`:

```csharp
app.MapScalarApiReference("/scalar", options =>
{
    options.WithTitle("APITemplate")
           .AddHttpAuthentication("Bearer", scheme =>
           {
               scheme.Token = string.Empty;
           });
});
```

The endpoint is only mounted in the `Development` environment (see `UseApiDocumentation()`). To expose it in staging/production, adjust the environment guard.

---

## Nitro – GraphQL IDE

The HotChocolate Nitro playground is available at:

```
http://localhost:5174/graphql/ui
```

The raw GraphQL endpoint (for programmatic access) is:

```
http://localhost:5174/graphql
```

### What You Can Do in Nitro

| Action | Description |
|--------|-------------|
| Write queries | Syntax-highlighted editor with autocompletion |
| Explore schema | Built-in schema browser and type documentation |
| Set headers | Add `Authorization` for mutation operations |
| View history | Saved queries from previous sessions |
| Inspect results | JSON result pane with collapsible nodes |

### Step-by-Step: Authenticate in Nitro

1. Open `http://localhost:5174/graphql/ui`
2. Click the **HTTP Headers** tab (bottom of the editor pane)
3. Add:
   ```json
   {
     "Authorization": "Bearer <your-jwt-token>"
   }
   ```
4. To get a token, first send a login request via Scalar (see above) or via the REST endpoint directly:
   ```bash
   curl -s -X POST http://localhost:5174/api/v1/auth/login \
     -H "Content-Type: application/json" \
     -d '{"username":"admin","password":"admin"}' \
   | grep -o '"accessToken":"[^"]*"'
   ```

### Example Queries

**List products (no auth required):**

```graphql
query {
  products(input: { pageNumber: 1, pageSize: 10 }) {
    items {
      id
      name
      price
      description
      createdAt
    }
    totalCount
    pageNumber
    pageSize
  }
}
```

**Get a product by ID:**

```graphql
query {
  productById(id: "3fa85f64-5717-4562-b3fc-2c963f66afa6") {
    id
    name
    price
    reviews {
      id
      reviewerName
      rating
      comment
      createdAt
    }
  }
}
```

**Filter products by price/sort/paging (using `ProductQueryInput`):**

```graphql
query {
  products(input: {
    minPrice: 10
    maxPrice: 100
    sortBy: "price"
    sortDirection: "asc"
    pageNumber: 1
    pageSize: 20
  }) {
    items {
      id
      name
      price
    }
    totalCount
  }
}
```

**Create a product (requires `Authorization` header in Nitro):**

```graphql
mutation {
  createProduct(input: {
    name: "My New Product"
    description: "Created from Nitro"
    price: 49.99
  }) {
    id
    name
    price
    createdAt
  }
}
```

**Delete a product (requires `Authorization` header):**

```graphql
mutation {
  deleteProduct(id: "3fa85f64-5717-4562-b3fc-2c963f66afa6")
}
```

### Using Variables in Nitro

Instead of hardcoding values, use the **Variables** tab:

**Query:**

```graphql
mutation CreateProduct($input: CreateProductRequestInput!) {
  createProduct(input: $input) {
    id
    name
    price
  }
}
```

**Variables:**

```json
{
  "input": {
    "name": "Widget",
    "description": "A useful widget",
    "price": 9.99
  }
}
```

---

## Health Check Endpoint

The health check endpoint is always available (no auth required):

```
GET http://localhost:5174/health
```

Example response:

```json
{
  "status": "Healthy",
  "services": [
    { "name": "postgresql", "status": "Healthy", "tags": ["database"] },
    { "name": "mongodb",    "status": "Healthy", "tags": ["database"] }
  ]
}
```

---

## Summary of Available UI Endpoints

| URL | Tool | Auth Required |
|-----|------|---------------|
| `/scalar/v1` | Scalar – REST UI | Needs a JWT once authenticated |
| `/openapi/v1.json` | Raw OpenAPI JSON | No |
| `/graphql/ui` | Nitro – GraphQL IDE | Needs a JWT for mutations |
| `/graphql` | GraphQL endpoint (programmatic) | Queries: No / Mutations: Yes |
| `/health` | Health check JSON | No |

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Extensions/ApplicationBuilderExtensions.cs` | `UseApiDocumentation()` — mounts Scalar and OpenAPI |
| `Api/OpenApi/HealthCheckOpenApiDocumentTransformer.cs` | Adds `/health` to the OpenAPI document |
| `Program.cs` | `app.MapGraphQL()` and `app.MapNitroApp("/graphql/ui")` |
| `appsettings.json` → `Bootstrap:Admin` and `Jwt` | Bootstrap login credentials and JWT settings |

