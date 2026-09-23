using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public class Court
{
    public int Id { get; set; }

    public int FutsalId { get; set; }
    public Futsal Futsal { get; set; } = null!;

    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? SurfaceType { get; set; }

    public TimeOnly OpeningTime { get; set; }

    public TimeOnly ClosingTime { get; set; }

    public int SlotDurationMinutes { get; set; } = 60;

    public decimal PricePerHour { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<SlotBlock> SlotBlocks { get; set; } = new List<SlotBlock>();
}