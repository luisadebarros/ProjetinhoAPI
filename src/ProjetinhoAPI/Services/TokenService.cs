using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ProjetinhoAPI.Configuration;
using ProjetinhoAPI.Models;
using ProjetinhoAPI.Security;
using ProjetinhoAPI.Services.Interfaces;

namespace ProjetinhoAPI.Services;

public sealed class TokenService(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider,
    ILogger<TokenService> logger) : ITokenService
{
    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateToken(AppUser user)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.ExpirationMinutes);

        var tokenId = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Jti, tokenId),
        };
        claims.AddRange(user.Roles.Select(role => new Claim(AuthClaims.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var token = _handler.CreateToken(descriptor);

        // Nunca registre o token em si: quem lê o log poderia usá-lo. O jti basta para rastrear.
        logger.LogDebug("Token JWT gerado para o usuário {Username} (jti {TokenId}), válido até {ExpiresAtUtc:u}",
            user.Username, tokenId, expires);

        return new AccessToken(token, expires);
    }
}
