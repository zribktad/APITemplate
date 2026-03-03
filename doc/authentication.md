# How Authentication Works (JWT)

This guide explains the JWT authentication flow used in the project, how to obtain a token, how to use it, and how to extend the implementation for production use.

---

## Overview

The project uses **ASP.NET Core JWT Bearer authentication**. The flow is:

```
Client  →  POST /api/v1/Auth/login  →  AuthController
                                            ↓
                                       UserService.ValidateAsync()
                                            ↓
                                       TokenService.GenerateToken()
                                            ↓
                                       { accessToken, expiresAt }
                                            ↓
Client  →  Authorization: Bearer <token>  →  Any protected endpoint
```

All controllers except `AuthController` require a valid token (`[Authorize]` attribute on the class). The GraphQL mutation classes use `[HotChocolate.Authorization.Authorize]`.

---

## Step 1 – Configure JWT Settings

Settings live in `appsettings.json` (and are overridden in `appsettings.Development.json`, which is **not** committed to source control):

```json
{
  "Jwt": {
    "Secret": "your-256-bit-or-longer-secret-key-here",
    "Issuer": "APITemplate",
    "Audience": "APITemplate",
    "ExpirationMinutes": "60"
  },
  "Auth": {
    "Username": "admin",
    "Password": "changeme"
  }
}
```

> ⚠️ **Never commit real secrets.** Use environment variables, Docker secrets, or a secrets manager (e.g., Azure Key Vault, AWS Secrets Manager) in production.

---

## Step 2 – Obtain a Token

```http
POST /api/v1/Auth/login
Content-Type: application/json

{
  "username": "admin",
  "password": "changeme"
}
```

**Response (200 OK):**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2024-01-01T13:00:00Z"
}
```

**Error (401 Unauthorized):** credentials did not match.

---

## Step 3 – Use the Token

Include the token in the `Authorization` header for all subsequent requests:

```http
GET /api/v1/Products
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

Or in the [Scalar](https://scalar.com/) / Swagger UI: click **Authorize** → enter the token (without the `Bearer ` prefix).

---

## How the Token Is Generated

`TokenService` (`Application/Services/TokenService.cs`) creates a signed JWT:

```csharp
public TokenResponse GenerateToken(string username)
{
    var jwtSettings = _configuration.GetSection("Jwt");
    var key = new SymmetricSecurityKey(
        Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));

    var expires = DateTime.UtcNow.AddMinutes(
        double.Parse(jwtSettings["ExpirationMinutes"]!));

    var claims = new[]
    {
        new Claim(ClaimTypes.Name, username),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

    var token = new JwtSecurityToken(
        issuer:             jwtSettings["Issuer"],
        audience:           jwtSettings["Audience"],
        claims:             claims,
        expires:            expires,
        signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

    return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(token), expires);
}
```

---

## How Token Validation Is Configured

Validation is registered in `ServiceCollectionExtensions.AddJwtAuthentication()`:

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSettings["Issuer"],
            ValidAudience            = jwtSettings["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(key)
        };
    });
```

`app.UseAuthentication()` and `app.UseAuthorization()` are called in `Program.cs` in the correct order.

---

## Protecting a New Endpoint

Add `[Authorize]` to the controller class (protects all actions) or to individual actions:

```csharp
// Entire controller requires a valid token
[ApiController]
[Authorize]
public sealed class OrdersController : ControllerBase { ... }

// Or allow anonymous access to specific actions
[HttpGet("public-summary")]
[AllowAnonymous]
public IActionResult GetPublicSummary() { ... }
```

For GraphQL mutations, use the HotChocolate attribute:

```csharp
using HotChocolate.Authorization;

[Authorize]
public class OrderMutations { ... }
```

---

## Reading the Authenticated User in a Controller

```csharp
[HttpGet("me")]
public IActionResult GetCurrentUser()
{
    var username = User.FindFirstValue(ClaimTypes.Name);
    return Ok(new { username });
}
```

---

## Replacing the Demo User Store (Production)

`UserService` currently validates against credentials stored in configuration — suitable only for demos. Replace it with a real user store:

### Option A – Database Users (EF Core)

```csharp
public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> ValidateAsync(string username, string password, CancellationToken ct)
    {
        var user = await _userRepository.GetByUsernameAsync(username, ct);
        if (user is null) return false;

        // Use a constant-time comparison library — never compare plain-text passwords.
        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }
}
```

### Option B – External Identity Provider (OAuth 2.0 / OpenID Connect)

Replace the local token generation entirely with an identity server (e.g., Keycloak, Auth0, Microsoft Entra ID) and configure `AddJwtBearer` to validate tokens issued by the external provider:

```csharp
options.Authority  = "https://your-identity-provider.com";
options.Audience   = "your-api-audience";
```

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Api/Controllers/V1/AuthController.cs` | Login endpoint |
| `Application/Services/TokenService.cs` | JWT generation |
| `Application/Services/UserService.cs` | Credential validation (replace in production) |
| `Application/DTOs/LoginRequest.cs` | Login request DTO |
| `Application/DTOs/TokenResponse.cs` | Token response DTO |
| `Extensions/ServiceCollectionExtensions.cs` | `AddJwtAuthentication()` |
| `Program.cs` | `app.UseAuthentication()` / `app.UseAuthorization()` |
| `appsettings.json` → `Jwt` section | Secret, issuer, audience, expiry |
