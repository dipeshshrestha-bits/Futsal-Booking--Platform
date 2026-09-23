namespace Backend.DTOs.Futsal;

/// <summary>Status values the frontend understands: Available, Booked, Blocked, Past.</summary>
public class SlotDto
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Status { get; set; } = "Available";
    public bool IsAvailable { get; set; }
    public decimal Price { get; set; }

    /// <summary>Why it's unavailable (block reason). Never exposes the player's details.</summary>
    public string? Reason { get; set; }
}

public class AvailabilityDto
{
    public int FutsalId { get; set; }
    public int CourtId { get; set; }
    public string CourtName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly OpeningTime { get; set; }
    public TimeOnly ClosingTime { get; set; }
    public int SlotDurationMinutes { get; set; }
    public decimal PricePerHour { get; set; }
    public List<SlotDto> Slots { get; set; } = new();
}