using ProjetinhoAPI.Models;

namespace ProjetinhoAPI.Services.Interfaces;

public interface ITokenService
{
    AccessToken CreateToken(AppUser user);
}
