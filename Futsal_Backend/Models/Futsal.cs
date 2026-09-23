using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public class Futsal
{
    public int Id { get; set; }

    public int OwnerId { get; set; }
    public User Owner { get; set; } = null!;

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

    [MaxLength(500)]
    public string? CoverImageUrl { get; set; }

    /// <summary>Venue-wide hours shown on the listing (each court also has its own hours).</summary>
    public TimeOnly? OpeningTime { get; set; }

    public TimeOnly? ClosingTime { get; set; }

    /// <summary>Stored as a PostgreSQL text[] column.</summary>
    public List<string> Amenities { get; set; } = new();

    public bool IsActive { get; set; } = true;

    /// <summary>False until the owner fills in name, address, city and contact number.</summary>
    public bool IsProfileComplete { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<FutsalImage> Images { get; set; } = new List<FutsalImage>();
    public ICollection<Court> Courts { get; set; } = new List<Court>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}