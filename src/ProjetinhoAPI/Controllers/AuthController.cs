using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjetinhoAPI.DTOs.Auth;
using ProjetinhoAPI.Repositories.Interfaces;
using ProjetinhoAPI.Security;
using ProjetinhoAPI.Services.Interfaces;

namespace ProjetinhoAPI.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(IUserRepository users, ITokenService tokens) : ControllerBase
{
    /// <summary>Troca usuário + senha por um token JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        var user = users.ValidateCredentials(request.Username, request.Password);
        if (user is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Usuário ou senha inválidos.");

        var token = tokens.CreateToken(user);
        return Ok(new LoginResponse(token.Token, "Bearer", token.ExpiresAtUtc));
    }

    /// <summary>Retorna os dados do usuário dono do token.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<MeResponse> Me()
    {
        var id = User.FindFirstValue(AuthClaims.UserId) ?? string.Empty;
        var username = User.Identity?.Name ?? string.Empty;
        var roles = User.FindAll(AuthClaims.Role).Select(c => c.Value);
        return Ok(new MeResponse(id, username, roles));
    }
}
