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

    private readonly ILogger<UserRepository> _logger;

    public UserRepository(ILogger<UserRepository> logger)
    {
        _logger = logger;
        _dummyHash = _hasher.HashPassword(new AppUser { Username = "dummy" }, Guid.NewGuid().ToString());
    }

    // Os motivos ficam só no log (lado do servidor). Para o cliente a resposta é sempre a mesma,
    // assim ninguém descobre quais usuários existem.
    public AppUser? ValidateCredentials(string username, string password)
    {
        if (!_users.TryGetValue(username, out var user))
        {
            _hasher.VerifyHashedPassword(new AppUser { Username = username }, _dummyHash, password);
            _logger.LogWarning("Login recusado: o usuário {Username} não existe", username);
            return null;
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Login recusado: senha incorreta para o usuário {Username}", username);
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password);
            _logger.LogInformation("Hash da senha do usuário {Username} atualizado para o formato mais recente", username);
        }

        return user;
    }

    public AppUser? FindById(Guid id) => _users.Values.FirstOrDefault(u => u.Id == id);

    public AppUser Add(string username, string password, params string[] roles)
    {
        var user = new AppUser { Username = username, Roles = roles };
        user.PasswordHash = _hasher.HashPassword(user, password);

        if (!_users.TryAdd(username, user))
        {
            _logger.LogWarning("Cadastro recusado: o usuário {Username} já existe", username);
            throw new InvalidOperationException($"Usuário '{username}' já existe.");
        }

        _logger.LogInformation("Usuário {Username} cadastrado com os papéis {Roles}", username, roles);
        return user;
    }
}
