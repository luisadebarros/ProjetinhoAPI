namespace ProjetinhoAPI.Models;

public sealed class AppUser
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Username { get; init; }
    public string PasswordHash { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = [];
}
