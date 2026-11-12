using System.ComponentModel.DataAnnotations;

namespace ProjetinhoAPI.DTOs.Auth;

public sealed record LoginRequest(
    [Required] string Username,
    [Required] string Password);
