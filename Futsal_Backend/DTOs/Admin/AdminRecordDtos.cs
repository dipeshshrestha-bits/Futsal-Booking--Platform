namespace Backend.DTOs.Admin;

/// <summary>Field names match what the React frontend reads.</summary>
public class AdminBookingListItemDto
{
    public int Id { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;

    public int FutsalId { get; set; }
    public string FutsalName { get; set; } = string.Empty;
    public string? FutsalAddress { get; set; }
    public string? City { get; set; }

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

    public DateTime CreatedAt { get; set; }
}

public class AdminTransactionDto
{
    public int Id { get; set; }
    public string? TransactionCode { get; set; }
    public string? GatewayRefId { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public int BookingId { get; set; }

    public string FutsalName { get; set; } = string.Empty;
    public string? City { get; set; }
    public string PlayerName { get; set; } = string.Empty;

    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AdminBookingFilterDto
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? City { get; set; }
    public string? Status { get; set; }
    public string? PaymentStatus { get; set; }
    public string? PaymentMethod { get; set; }
    public int? FutsalId { get; set; }
    public string? Search { get; set; }
}

public class AdminTransactionFilterDto
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Method { get; set; }
    public string? Status { get; set; }
    public int? FutsalId { get; set; }
}