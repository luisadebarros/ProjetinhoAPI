using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using ProjetinhoAPI.Models;
using ProjetinhoAPI.Repositories.Interfaces;

namespace ProjetinhoAPI.Repositories;

/// <summary>
/// Armazenamento TEMPORÁRIO em memória (os dados somem ao reiniciar a API).
/// Será substituído por um repositório com banco de dados.
/// As senhas são guardadas com hash (PBKDF2 via PasswordHasher do ASP.NET Core Identity).
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, AppUser> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly PasswordHasher<AppUser> _hasher = new();

    // Hash "falso" usado quando o usuário não existe, para que o tempo de resposta
    // seja parecido com o de um usuário existente (evita descobrir usuários pelo tempo).
    private readonly string _dummyHash;

    public UserRepository()
    {
        _dummyHash = _hasher.HashPassword(new AppUser { Username = "dummy" }, Guid.NewGuid().ToString());
    }

    public AppUser? ValidateCredentials(string username, string password)
    {
        if (!_users.TryGetValue(username, out var user))
        {
            _hasher.VerifyHashedPassword(new AppUser { Username = username }, _dummyHash, password);
            return null;
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
            return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = _hasher.HashPassword(user, password);

        return user;
    }

    public AppUser? FindById(Guid id) => _users.Values.FirstOrDefault(u => u.Id == id);

    public AppUser Add(string username, string password, params string[] roles)
    {
        var user = new AppUser { Username = username, Roles = roles };
        user.PasswordHash = _hasher.HashPassword(user, password);

        if (!_users.TryAdd(username, user))
            throw new InvalidOperationException($"Usuário '{username}' já existe.");

        return user;
    }
}
