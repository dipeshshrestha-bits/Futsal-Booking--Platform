using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs.Owner;

public class CourtDto
{
    public int Id { get; set; }
    public int FutsalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SurfaceType { get; set; }
    public TimeOnly OpeningTime { get; set; }
    public TimeOnly ClosingTime { get; set; }
    public int SlotDurationMinutes { get; set; }
    public decimal PricePerHour { get; set; }
    public bool IsActive { get; set; }
    public int UpcomingBookings { get; set; }
}

public class SaveCourtDto
{
    [Required(ErrorMessage = "Court name is required.")]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? SurfaceType { get; set; }

    [Required]
    public TimeOnly OpeningTime { get; set; }

    [Required]
    public TimeOnly ClosingTime { get; set; }

    [Range(15, 240, ErrorMessage = "Slot length must be between 15 and 240 minutes.")]
    public int SlotDurationMinutes { get; set; } = 60;

    [Range(0, 100000, ErrorMessage = "Enter a price between 0 and 100000.")]
    public decimal PricePerHour { get; set; }

    public bool IsActive { get; set; } = true;
}

public class BlockSlotDto
{
    [Required]
    public int CourtId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    /// <summary>Leave both times empty to block the whole day.</summary>
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }

    [MaxLength(200)]
    public string? Reason { get; set; }
}

public class SlotBlockDto
{
    public int Id { get; set; }
    public int CourtId { get; set; }
    public string CourtName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string? Reason { get; set; }
}