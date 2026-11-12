namespace ProjetinhoAPI.DTOs.Auth;

public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAtUtc);
