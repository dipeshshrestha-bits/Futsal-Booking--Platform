using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public class Review
{
    public int Id { get; set; }

    public int FutsalId { get; set; }
    public Futsal Futsal { get; set; } = null!;

    /// <summary>Set when reviews are verified against a real booking (default).</summary>
    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }

    [MaxLength(80)]
    public string PlayerName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string PlayerContact { get; set; } = string.Empty;

    public int Rating { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }

    [MaxLength(500)]
    public string? OwnerReply { get; set; }

    public DateTime? OwnerReplyAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}