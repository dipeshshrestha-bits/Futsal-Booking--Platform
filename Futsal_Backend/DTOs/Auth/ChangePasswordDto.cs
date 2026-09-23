using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs.Auth;

public class ChangePasswordDto
{
    [Required(ErrorMessage = "Enter your current password.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a new password.")]
    [MinLength(8, ErrorMessage = "New password must be at least 8 characters.")]
    [MaxLength(100)]
    public string NewPassword { get; set; } = string.Empty;
}