using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs.Owner;

public class FutsalProfileDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public TimeOnly? OpeningTime { get; set; }
    public TimeOnly? ClosingTime { get; set; }
    public List<string> Amenities { get; set; } = new();
    public string? CoverImageUrl { get; set; }
    public List<string> Images { get; set; } = new();

    public bool IsActive { get; set; }
    public bool IsProfileComplete { get; set; }
    public bool IsListedPublicly { get; set; }
    public int CourtCount { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

public class UpdateFutsalProfileDto
{
    [Required(ErrorMessage = "Venue name is required.")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? City { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    [MaxLength(20)]
    public string? ContactNumber { get; set; }

    [MaxLength(150)]
    public string? Email { get; set; }

    public TimeOnly? OpeningTime { get; set; }
    public TimeOnly? ClosingTime { get; set; }

    public List<string>? Amenities { get; set; }

    [MaxLength(500)]
    public string? CoverImageUrl { get; set; }

    public List<string>? Images { get; set; }
}