using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ProjetinhoAPI.Security;

/// <summary>
/// Eventos da autenticação JWT usados para registrar no log os tokens recusados
/// (expirado, assinatura inválida etc.) e os acessos negados por falta de permissão.
/// </summary>
public sealed class JwtBearerLoggingEvents(ILogger<JwtBearerLoggingEvents> logger) : JwtBearerEvents
{
    public override Task AuthenticationFailed(AuthenticationFailedContext context)
    {
        logger.LogWarning("Token JWT recusado em {Method} {Path}: {Reason}",
            context.Request.Method, context.Request.Path, DescribeFailure(context.Exception));

        return base.AuthenticationFailed(context);
    }

    public override Task Forbidden(ForbiddenContext context)
    {
        logger.LogWarning("Acesso negado (403) para o usuário {Username} em {Method} {Path}",
            context.HttpContext.User.Identity?.Name ?? "anônimo", context.Request.Method, context.Request.Path);

        return base.Forbidden(context);
    }

    public override Task TokenValidated(TokenValidatedContext context)
    {
        logger.LogDebug("Token JWT válido para o usuário {Username}", context.Principal?.Identity?.Name);

        return base.TokenValidated(context);
    }

    private static string DescribeFailure(Exception exception) => exception switch
    {
        SecurityTokenExpiredException => "token expirado",
        SecurityTokenNotYetValidException => "token ainda não é válido",
        SecurityTokenInvalidSignatureException => "assinatura inválida",
        SecurityTokenInvalidIssuerException => "emissor (iss) inválido",
        SecurityTokenInvalidAudienceException => "audiência (aud) inválida",
        SecurityTokenInvalidAlgorithmException => "algoritmo não permitido",
        SecurityTokenMalformedException => "token malformado",
        _ => exception.GetType().Name,
    };
}
