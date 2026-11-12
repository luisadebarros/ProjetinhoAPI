namespace ProjetinhoAPI.Models;

/// <summary>Token gerado pelo TokenService e a data em que ele expira.</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);
