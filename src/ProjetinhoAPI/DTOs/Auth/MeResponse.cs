namespace ProjetinhoAPI.DTOs.Auth;

public sealed record MeResponse(string Id, string Username, IEnumerable<string> Roles);
