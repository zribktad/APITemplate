namespace APITemplate.Application.Features.Auth.DTOs;
public sealed record TokenResponse(string AccessToken, DateTime ExpiresAt);
