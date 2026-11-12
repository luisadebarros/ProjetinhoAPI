using Microsoft.IdentityModel.JsonWebTokens;

namespace ProjetinhoAPI.Security;

/// <summary>
/// Nomes curtos das claims usadas no token. Como desligamos o mapeamento automático
/// (MapInboundClaims = false), as claims chegam no User com estes mesmos nomes.
/// </summary>
public static class AuthClaims
{
    public const string UserId = JwtRegisteredClaimNames.Sub;
    public const string Username = JwtRegisteredClaimNames.UniqueName;
    public const string Role = "role";
}
