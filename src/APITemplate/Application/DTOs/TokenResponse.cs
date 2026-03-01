namespace APITemplate.Application.DTOs;

public sealed record TokenResponse(string AccessToken, DateTime ExpiresAt);
