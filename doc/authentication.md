# How Authentication Works (Authentik OIDC + Tenant Claims)

This guide explains the authentication architecture: Authentik as the Identity Provider, two authentication flows (JWT Bearer for APIs, Cookie+OIDC for browser SPAs), tenant claim enforcement, and endpoint protection.

---

## Overview

Authentication is delegated to **Authentik** (open-source Identity Provider). The application supports two authentication flows via two separate Authentik providers:

| Flow | Scheme | Clients | Provider |
|------|--------|---------|----------|
| **JWT Bearer** | `JwtBearerDefaults.AuthenticationScheme` | Mobile apps, server-to-server, Swagger/Scalar | `api-template` |
| **BFF Cookie + OIDC** | `BffCookie` + `BffOidc` | Browser SPA (React, Vue, etc.) | `api-template-bff` |

Both flows enforce a valid `tenant_id` claim — tokens/sessions without it are rejected.

---

## Flow 1: JWT Bearer (API Clients)

```
Client  ->  POST /api/v1/Auth/login  ->  AuthController
                                          |
                                          v
                                   AuthentikAuthenticationProxy
                                          |  (ROPC grant: username, password)
                                          v
                                   Authentik Token Endpoint
                                          |
                                          v
                                   { accessToken, expiresAt }
                                          |
Client  ->  Authorization: Bearer <token>  ->  Protected REST/GraphQL endpoint
                                                  |
                                                  v
                                           JwtBearer middleware validates
                                           RS256 signature via OIDC discovery
                                           + enforces tenant_id claim
```

### Obtain a Token

```http
POST /api/v1/Auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "admin-password"
}
```

The API proxies this to Authentik's token endpoint using the ROPC grant with configured `ClientId` and `ClientSecret`.

**Success (200):**

```json
{
  "accessToken": "<jwt>",
  "expiresAt": "2026-03-04T12:00:00Z"
}
```

**Failure (401):** `LoginErrorResponse` when credentials are invalid or Authentik is unreachable.

### Use the Token

```http
GET /api/v1/Products
Authorization: Bearer <jwt>
```

REST controllers are protected with `[Authorize]` (except `AuthController.Login`).
GraphQL query and mutation fields are protected with `[Authorize]`.

---

## Flow 2: BFF Cookie + OIDC (Browser SPA Clients)

The BFF (Backend-for-Frontend) pattern keeps tokens server-side and uses secure HttpOnly cookies for browser sessions. The SPA never handles tokens directly.

```
Browser  ->  GET /bff/login  ->  302 Redirect to Authentik authorize endpoint
                                          |  (Authorization Code + PKCE)
                                          v
                                   Authentik login page
                                          |  (user authenticates)
                                          v
                                   302 Redirect back with auth code
                                          |
                                   Server exchanges code for tokens
                                          |
                                   Sets BffCookie (HttpOnly, SameSite=Lax)
                                          |
Browser  ->  GET /bff/user  ->  200 { sub, email, roles, tenantId, ... }
                                          + X-XSRF-TOKEN response header
                                          |
Browser  ->  POST /bff/logout  ->  Signs out Cookie + OIDC
             (with X-XSRF-TOKEN header)    302 Redirect to /
```

### BFF Endpoints

| Endpoint | Method | Auth | Purpose |
|----------|--------|------|---------|
| `/bff/login?returnUrl=` | GET | Anonymous | Initiates OIDC login. `returnUrl` must be a local URL (open redirect protection). |
| `/bff/user` | GET | `BffCookie` | Returns authenticated user info + sets `X-XSRF-TOKEN` response header. |
| `/bff/logout` | POST | `BffCookie` | Signs out. Requires `X-XSRF-TOKEN` header (CSRF protection). Returns 400 if token invalid. |

### User Response

`GET /bff/user` returns:

```json
{
  "sub": "d4f8a2b1-...",
  "preferredUsername": "admin",
  "email": "admin@example.com",
  "name": "Admin User",
  "tenantId": "c3e9f1a0-...",
  "roles": ["PlatformAdmin"]
}
```

### CSRF Protection

The `/bff/user` endpoint returns an `X-XSRF-TOKEN` response header. The SPA must:
1. Read the token from the response header
2. Send it back as `X-XSRF-TOKEN` request header on `POST /bff/logout`

The antiforgery cookie (`.APITemplate.Antiforgery`) is set automatically and sent by the browser.

### CORS

CORS is configured with `.AllowCredentials()` to allow cross-origin cookie sending from the SPA:

```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "http://localhost:5173"]
  }
}
```

---

## Token Claims

Authentik issues these claims (configured via provider + property mapping):

| Claim | Type Constant | Description |
|-------|---------------|-------------|
| `sub` | `JwtRegisteredClaimNames.Sub` | User ID (Authentik user UUID) |
| `tenant_id` | `CustomClaimTypes.TenantId` | Tenant ID (custom property mapping, required) |
| `groups` | — | User groups/roles (`PlatformAdmin`, `TenantUser`) |
| `preferred_username` | — | Username |
| `email` | — | User email |
| `name` | — | Display name |
| `jti` | `JwtRegisteredClaimNames.Jti` | Token ID |

### Tenant Claim Validation

`TenantClaimValidator.HasValidTenantClaim()` enforces:
- Claim `tenant_id` must be present
- Value must parse as a valid GUID
- GUID must not be `Guid.Empty`

This validation runs in three places:
- **JWT Bearer:** `OnTokenValidated` event — rejects token
- **BFF OIDC:** `OnTokenValidated` event — rejects token
- **BFF Cookie:** `OnValidatePrincipal` event — rejects principal (session invalidated)

---

## Multi-Tenancy

Tenant context is resolved per-request from the authenticated user's `tenant_id` claim.

| Interface | Implementation | Purpose |
|-----------|---------------|---------|
| `ITenantProvider` | `HttpTenantProvider` | Resolves `TenantId` (Guid) and `HasTenant` (bool) from `HttpContext.User` |
| `IActorProvider` | `HttpActorProvider` | Resolves `ActorId` (string) from user claims for audit trails |

Both are registered as scoped services.

---

## Authorization & Roles

```csharp
// Roles mapped from Authentik "groups" claim
public enum UserRole { TenantUser = 0, PlatformAdmin = 1 }

// Authorization policy
[Authorize(Policy = AuthorizationPolicies.PlatformAdminOnly)]
```

---

## Reading Claims in Code

```csharp
// In a controller
var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
var tenantId = User.FindFirstValue(CustomClaimTypes.TenantId);
// Roles are read from "groups" claim (mapped via RoleClaimType)

// Via DI
var tenantId = tenantProvider.TenantId;    // ITenantProvider
var actorId = actorProvider.ActorId;        // IActorProvider
```

---

## Protecting a New Endpoint

**REST (JWT Bearer — default scheme):**

```csharp
[ApiController]
[Authorize]
public sealed class OrdersController : ControllerBase { }
```

**REST (BFF Cookie):**

```csharp
[Authorize(AuthenticationSchemes = BffAuthenticationSchemes.Cookie)]
public IActionResult GetDashboard() { }
```

**GraphQL:**

```csharp
[Authorize]
public class OrderMutations { }
```

**Platform Admin only:**

```csharp
[Authorize(Policy = AuthorizationPolicies.PlatformAdminOnly)]
public IActionResult AdminEndpoint() { }
```

---

## Configuration

### appsettings.json

```json
{
  "Authentik": {
    "Authority": "",
    "ClientId": "",
    "ClientSecret": "",
    "TokenEndpoint": "",
    "TenantClaimType": "tenant_id",
    "RoleClaimType": "groups"
  },
  "Bff": {
    "Authority": "",
    "ClientId": "",
    "ClientSecret": "",
    "CookieName": ".APITemplate.Bff",
    "PostLogoutRedirectUri": "/",
    "SessionTimeoutMinutes": 60,
    "Scopes": ["openid", "profile", "email", "api"]
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000"]
  },
  "Bootstrap": {
    "Tenant": { "Code": "default", "Name": "Default Tenant" }
  },
  "SystemIdentity": {
    "DefaultActorId": "system"
  }
}
```

### Development

`appsettings.Development.json` points to local Authentik on port 9000:

```json
{
  "Authentik": {
    "Authority": "http://localhost:9000/application/o/api-template/",
    "ClientId": "api-template",
    "ClientSecret": "dev-client-secret",
    "TokenEndpoint": "http://localhost:9000/application/o/token/"
  },
  "Bff": {
    "Authority": "http://localhost:9000/application/o/api-template-bff/",
    "ClientId": "api-template-bff",
    "ClientSecret": "dev-bff-client-secret"
  }
}
```

### Production

All `Authentik:*` and `Bff:*` values must be supplied securely (env vars/secret manager). The app validates options with `ValidateOnStart()` and fails fast if required values are missing.

---

## Authentik Setup (Manual)

After `docker-compose up`, configure Authentik at `http://localhost:9000`:

### Provider 1: API (JWT Bearer)

1. Create OAuth2/OIDC Provider: `api-template`, Client Type: Confidential, Signing Key: RS256
2. Create an Application linked to the provider
3. Create a Property Mapping (scope `api`):
   ```python
   return {"tenant_id": request.user.attributes.get("tenant_id", "")}
   ```
4. Create Authentik groups: `PlatformAdmin`, `TenantUser`
5. Create a user with custom attribute `tenant_id: "<guid>"` matching `Tenant.Id` in the app DB
6. Enable ROPC grant on the provider (for the login proxy)

### Provider 2: BFF (Cookie + OIDC)

1. Create OAuth2/OIDC Provider: `api-template-bff`, Client Type: Confidential, Signing Key: RS256
2. Redirect URI: `http://localhost:5000/signin-oidc`
3. Post-logout redirect URI: `http://localhost:5000/signout-callback-oidc`
4. Grant type: Authorization Code, PKCE enabled
5. Assign the same `api` scope with `tenant_id` property mapping
6. Assign groups: `PlatformAdmin`, `TenantUser`

---

## Bootstrap Seeding

Startup runs `AuthBootstrapSeeder` (via `UseDatabaseAsync()`), which ensures the bootstrap tenant exists in the database. If missing, it creates it. If soft-deleted/inactive, it reactivates it.

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Api/Controllers/V1/AuthController.cs` | Login endpoint (proxies to Authentik via ROPC) |
| `Api/Controllers/V1/BffController.cs` | BFF login/logout/user endpoints for browser SPA |
| `Application/Features/Auth/Interfaces/IAuthenticationProxy.cs` | Authentication proxy interface |
| `Application/Features/Auth/DTOs/LoginRequest.cs` | Login request DTO |
| `Application/Features/Auth/DTOs/TokenResponse.cs` | JWT token response DTO |
| `Application/Features/Bff/DTOs/BffUserResponse.cs` | Authenticated user info for BFF |
| `Application/Common/Options/AuthentikOptions.cs` | JWT Bearer auth options |
| `Application/Common/Options/BffOptions.cs` | BFF Cookie+OIDC auth options |
| `Application/Common/Security/CustomClaimTypes.cs` | `tenant_id` claim type constant |
| `Application/Common/Security/BffAuthenticationSchemes.cs` | `BffCookie`, `BffOidc` scheme names |
| `Application/Common/Security/AuthorizationPolicies.cs` | `PlatformAdminOnly` policy name |
| `Infrastructure/Security/AuthentikAuthenticationProxy.cs` | ROPC grant implementation |
| `Infrastructure/Security/TenantClaimValidator.cs` | Shared tenant claim validation |
| `Infrastructure/Security/HttpTenantProvider.cs` | Per-request tenant resolution |
| `Infrastructure/Security/HttpActorProvider.cs` | Per-request actor ID resolution |
| `Infrastructure/Persistence/AuthBootstrapSeeder.cs` | Startup bootstrap seed for tenant |
| `Extensions/ServiceCollectionExtensions.cs` | `AddAuthentikAuthentication()`, `AddBffAuthentication()` |
| `appsettings.json` | Base config |
| `appsettings.Development.json` | Development overrides (local Authentik) |
