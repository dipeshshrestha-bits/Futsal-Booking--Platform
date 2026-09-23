using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public enum PaymentMethod
{
    Cash,
    Online,
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Refunded,
}

public enum BookingStatus
{
    Confirmed,
    Cancelled,
    Completed,
    NoShow,
}

public enum BookingSource
{
    PlayerOnline,
    OwnerWalkIn,
    OwnerPhone,
}

public class Booking
{
    public int Id { get; set; }

    public int CourtId { get; set; }
    public Court Court { get; set; } = null!;

    /// <summary>Local venue date.</summary>
    public DateOnly BookingDate { get; set; }

    /// <summary>Local venue time.</summary>
    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    [MaxLength(20)]
    public string ReferenceCode { get; set; } = string.Empty;

    [MaxLength(80)]
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>Normalized 10-digit mobile number.</summary>
    [MaxLength(20)]
    public string PlayerContact { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? PlayerEmail { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public BookingStatus BookingStatus { get; set; } = BookingStatus.Confirmed;

    public BookingSource Source { get; set; } = BookingSource.PlayerOnline;

    public decimal TotalAmount { get; set; }

    [MaxLength(200)]
    public string? Notes { get; set; }

    [MaxLength(200)]
    public string? CancellationReason { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public Review? Review { get; set; }
}