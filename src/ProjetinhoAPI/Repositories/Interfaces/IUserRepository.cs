using ProjetinhoAPI.Models;

namespace ProjetinhoAPI.Repositories.Interfaces;

public interface IUserRepository
{
    /// <summary>Retorna o usuário se as credenciais forem válidas; caso contrário, null.</summary>
    AppUser? ValidateCredentials(string username, string password);
    AppUser? FindById(Guid id);
    AppUser Add(string username, string password, params string[] roles);
}
