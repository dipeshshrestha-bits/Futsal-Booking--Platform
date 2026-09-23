using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public enum UserRole
{
    Admin,
    Owner,
}

public class User
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Stored lowercase; unique.</summary>
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Stored lowercase; unique.</summary>
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public bool IsActive { get; set; } = true;

    public bool MustChangePassword { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    /// <summary>Owners have exactly one futsal; admins have none.</summary>
    public Futsal? Futsal { get; set; }
}