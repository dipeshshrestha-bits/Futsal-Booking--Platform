using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs.Booking;

public class CreateBookingDto
{
    public int? FutsalId { get; set; }

    [Required]
    public int CourtId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    [Required(ErrorMessage = "Your name is required.")]
    [MaxLength(80)]
    public string PlayerName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ContactNumber { get; set; }

    /// <summary>Alias for the spec's field name.</summary>
    [MaxLength(20)]
    public string? PlayerContact { get; set; }

    [MaxLength(150)]
    public string? Email { get; set; }

    [MaxLength(150)]
    public string? PlayerEmail { get; set; }

    /// <summary>"Cash" or "Esewa" (also accepts "Online").</summary>
    [Required(ErrorMessage = "Choose a payment method.")]
    public string PaymentMethod { get; set; } = "Cash";

    public string? Contact => !string.IsNullOrWhiteSpace(ContactNumber) ? ContactNumber : PlayerContact;
    public string? EmailAddress => !string.IsNullOrWhiteSpace(Email) ? Email : PlayerEmail;
}

public class BookingResponseDto
{
    public int Id { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;

    public int FutsalId { get; set; }
    public string FutsalName { get; set; } = string.Empty;
    public string? FutsalAddress { get; set; }
    public string? FutsalContact { get; set; }

    public int CourtId { get; set; }
    public string CourtName { get; set; } = string.Empty;

    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public string PlayerName { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string? Email { get; set; }

    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public bool CanCancel { get; set; }
    public bool CanReview { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Present only for eSewa bookings: the frontend auto-submits this form.</summary>
    public EsewaFormDto? Esewa { get; set; }
}

/// <summary>Matches what the frontend's submitEsewaForm() expects.</summary>
public class EsewaFormDto
{
    public string FormUrl { get; set; } = string.Empty;
    public Dictionary<string, string> Fields { get; set; } = new();
}