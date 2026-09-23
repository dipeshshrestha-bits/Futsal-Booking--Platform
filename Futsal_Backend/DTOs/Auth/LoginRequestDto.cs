using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs.Auth;

/// <summary>Accepts either a username or an email as the identifier.</summary>
public class LoginRequestDto
{
    [MaxLength(150)]
    public string? Username { get; set; }

    [MaxLength(150)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;

    public string? Identifier =>
        !string.IsNullOrWhiteSpace(Username) ? Username.Trim()
        : !string.IsNullOrWhiteSpace(Email) ? Email.Trim()
        : null;
}