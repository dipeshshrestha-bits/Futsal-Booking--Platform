namespace Backend.DTOs.Auth;

public class AuthUserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;

    /// <summary>Owners only.</summary>
    public int? FutsalId { get; set; }
    public string? FutsalName { get; set; }
    public bool? IsProfileComplete { get; set; }
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
    public DateTime ExpiresAt { get; set; }
    public AuthUserDto User { get; set; } = new();
}