using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs.Owner;

/// <summary>Field names match what the React frontend reads.</summary>
public class OwnerBookingDto
{
    public int Id { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;

    public int FutsalId { get; set; }
    public string FutsalName { get; set; } = string.Empty;
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
    public string Source { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public bool CanCancel { get; set; }
    public bool CanMarkPaid { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ManualBookingDto
{
    [Required]
    public int CourtId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    public TimeOnly StartTime { get; set; }

    /// <summary>Optional: defaults to one slot length after StartTime.</summary>
    public TimeOnly? EndTime { get; set; }

    [Required(ErrorMessage = "Player name is required.")]
    [MaxLength(80)]
    public string PlayerName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? ContactNumber { get; set; }

    /// <summary>Alias accepted for the spec's field name.</summary>
    [MaxLength(20)]
    public string? PlayerContact { get; set; }

    [MaxLength(150)]
    public string? Email { get; set; }

    /// <summary>Optional override; otherwise calculated from the court's hourly price.</summary>
    public decimal? Amount { get; set; }

    public bool IsPaid { get; set; }

    /// <summary>"OwnerWalkIn" (default) or "OwnerPhone".</summary>
    public string? Source { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }

    public string? Contact => !string.IsNullOrWhiteSpace(ContactNumber) ? ContactNumber : PlayerContact;
}

public class CancelBookingDto
{
    [MaxLength(200)]
    public string? Reason { get; set; }
}

public class OwnerReviewDto
{
    public int Id { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? Reply { get; set; }
    public DateTime? RepliedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? BookingReference { get; set; }
}

public class ReviewReplyDto
{
    /// <summary>Frontend sends "reply"; the spec calls it "replyText". Both work.</summary>
    [MaxLength(500)]
    public string? Reply { get; set; }

    [MaxLength(500)]
    public string? ReplyText { get; set; }

    public string? Text => !string.IsNullOrWhiteSpace(Reply) ? Reply : ReplyText;
}