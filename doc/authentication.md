# How Authentication Works (JWT + Tenant Claims)

This guide explains the current authentication flow used in the project: bootstrap-seeded users, JWT issuance, tenant claim validation, and endpoint protection.

---

## Overview

Authentication is database-backed and seeded at startup:

```
Client  ->  POST /api/v1/Auth/login  ->  AuthController
                                          |
                                          v
                                   UserService.AuthenticateAsync()
                                          |
                                          v
                                   TokenService.GenerateToken()
                                          |
                                          v
                                   { accessToken, expiresAt }
                                          |
Client  ->  Authorization: Bearer <token>  ->  Protected REST/GraphQL mutation endpoint
```

Startup (`UseDatabaseAsync`) runs `AuthBootstrapSeeder`, which ensures a default tenant and admin user exist.

---

## Step 1 - Configure Authentication Settings

`Jwt` values control token signing/validation:

```json
{
  "Jwt": {
    "Secret": "",
    "Issuer": "APITemplate",
    "Audience": "APITemplate.Clients",
    "ExpirationMinutes": 60
  }
}
```

`Bootstrap` values control initial seeded credentials:

```json
{
  "Bootstrap": {
    "Admin": {
      "Username": "admin",
      "Password": "admin",
      "Email": "admin@example.com",
      "IsPlatformAdmin": true
    },
    "Tenant": {
      "Code": "default",
      "Name": "Default Tenant"
    }
  }
}
```

`SystemIdentity` is also validated on startup:

```json
{
  "SystemIdentity": {
    "DefaultActorId": "system"
  }
}
```

### Development

`appsettings.Development.json` includes a development-only JWT secret so local startup works without extra setup.

### Production

`Jwt:Secret` must be supplied securely (env var/secret manager). The app validates options with `ValidateOnStart()` and fails fast if required values are missing.

---

## Step 2 - Obtain a Token

```http
POST /api/v1/Auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "admin"
}
```

Success:

```json
{
  "accessToken": "<jwt>",
  "expiresAt": "2026-03-04T12:00:00Z"
}
```

Failure:
- `401 Unauthorized` with `LoginErrorResponse` when credentials are invalid.

---

## Step 3 - Use the Token

Use `Authorization: Bearer <token>` for protected endpoints:

```http
GET /api/v1/Products
Authorization: Bearer <jwt>
```

REST controllers are protected with `[Authorize]` (except `AuthController.Login`).
GraphQL mutations are protected with `[Authorize]`; queries are currently anonymous.

---

## Current Token Claims

`TokenService` issues these claims:
- `sub` -> user id
- `tenant_id` -> tenant id (required)
- `role` -> user role (`PlatformAdmin` / `TenantUser`)
- `jti` -> token id

`JwtBearerOptions.OnTokenValidated` rejects tokens missing a valid `tenant_id` claim.

---

## Reading Claims in Code

```csharp
var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
var tenantId = User.FindFirstValue(CustomClaimTypes.TenantId);
var role = User.FindFirstValue(ClaimTypes.Role);
```

---

## Protecting a New Endpoint

REST:

```csharp
[ApiController]
[Authorize]
public sealed class OrdersController : ControllerBase { }
```

GraphQL mutation:

```csharp
[Authorize]
public class OrderMutations { }
```

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Api/Controllers/V1/AuthController.cs` | Login endpoint |
| `Application/Features/Auth/Services/UserService.cs` | Username/password authentication against seeded `AppUser` |
| `Application/Features/Auth/Services/TokenService.cs` | JWT generation |
| `Application/Common/Security/CustomClaimTypes.cs` | `tenant_id` claim type constant |
| `Application/Common/Options/JwtOptions.cs` | Strongly-typed JWT options |
| `Application/Common/Options/BootstrapAdminOptions.cs` | Seeded admin options |
| `Application/Common/Options/BootstrapTenantOptions.cs` | Seeded tenant options |
| `Infrastructure/Persistence/AuthBootstrapSeeder.cs` | Startup bootstrap seed for tenant/admin |
| `Extensions/ServiceCollectionExtensions.cs` | `AddAuthenticationOptions()` + `AddJwtAuthentication()` |
| `Extensions/ApplicationBuilderExtensions.cs` | `UseDatabaseAsync()` runs seeding |
| `appsettings.json` | Base config (`Jwt`, `Bootstrap`, `SystemIdentity`, `Cors`) |
| `appsettings.Development.json` | Development overrides (dev JWT secret) |
