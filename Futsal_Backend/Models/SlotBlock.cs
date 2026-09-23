using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public class SlotBlock
{
    public int Id { get; set; }

    public int CourtId { get; set; }
    public Court Court { get; set; } = null!;

    public DateOnly Date { get; set; }

    /// <summary>Null StartTime and EndTime means the whole day is blocked.</summary>
    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    [MaxLength(200)]
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}