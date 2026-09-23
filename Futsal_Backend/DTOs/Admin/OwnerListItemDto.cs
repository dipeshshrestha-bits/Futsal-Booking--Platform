namespace Backend.DTOs.Admin;

public class OwnerListItemDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public int? FutsalId { get; set; }
    public string? FutsalName { get; set; }
    public string? City { get; set; }
    public bool IsProfileComplete { get; set; }
    public bool FutsalIsActive { get; set; }
    public int CourtCount { get; set; }
    public int BookingCount { get; set; }
}

public class AdminFutsalListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? ContactNumber { get; set; }
    public bool IsActive { get; set; }
    public bool IsProfileComplete { get; set; }
    public bool IsListedPublicly { get; set; }
    public DateTime CreatedAt { get; set; }

    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public bool OwnerIsActive { get; set; }

    public int CourtCount { get; set; }
    public int BookingCount { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}