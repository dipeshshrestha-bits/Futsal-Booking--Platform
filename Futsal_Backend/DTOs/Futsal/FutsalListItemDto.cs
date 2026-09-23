namespace Backend.DTOs.Futsal;

public class FutsalListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? CoverImageUrl { get; set; }
    public decimal? StartingPrice { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int CourtCount { get; set; }
    public TimeOnly? OpeningTime { get; set; }
    public TimeOnly? ClosingTime { get; set; }
}

public class PublicCourtDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SurfaceType { get; set; }
    public TimeOnly OpeningTime { get; set; }
    public TimeOnly ClosingTime { get; set; }
    public int SlotDurationMinutes { get; set; }
    public decimal PricePerHour { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ReviewDto
{
    public int Id { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? Reply { get; set; }
    public DateTime? RepliedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FutsalDetailDto : FutsalListItemDto
{
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public List<string> Amenities { get; set; } = new();
    public List<string> Images { get; set; } = new();
    public List<PublicCourtDto> Courts { get; set; } = new();
    public List<ReviewDto> Reviews { get; set; } = new();
}

public class FutsalFilterDto
{
    public string? City { get; set; }
    public string? Search { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    /// <summary>"price_asc", "price_desc", "rating" or "name".</summary>
    public string? Sort { get; set; }
}