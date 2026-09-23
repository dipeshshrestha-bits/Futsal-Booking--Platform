using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs.Admin;

public class CreateOwnerDto
{
    [Required(ErrorMessage = "Owner full name is required.")]
    [MaxLength(80)]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Optional: generated from the email when omitted.</summary>
    [MaxLength(50)]
    public string? Username { get; set; }

    [Required(ErrorMessage = "Phone number is required.")]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Futsal name is required.")]
    [MaxLength(100)]
    public string FutsalName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? City { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }
}

/// <summary>Returned once, right after creating an owner or resetting a password.</summary>
public class OwnerCredentialsDto
{
    public int OwnerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TemporaryPassword { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = string.Empty;
    public string? FutsalName { get; set; }
    public int? FutsalId { get; set; }
}

public class StatusUpdateDto
{
    [Required]
    public bool IsActive { get; set; }
}