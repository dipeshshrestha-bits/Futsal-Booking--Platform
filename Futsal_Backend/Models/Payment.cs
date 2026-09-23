using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public enum TransactionMethod
{
    Cash,
    Esewa,
}

public enum TransactionStatus
{
    Initiated,
    Success,
    Failed,
}

/// <summary>One row per payment attempt (online) or cash collection.</summary>
public class Payment
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public decimal Amount { get; set; }

    public TransactionMethod Method { get; set; }

    /// <summary>transaction_uuid sent to eSewa.</summary>
    [MaxLength(100)]
    public string? GatewayTransactionUuid { get; set; }

    /// <summary>ref_id / transaction_code returned by eSewa.</summary>
    [MaxLength(100)]
    public string? GatewayRefId { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Initiated;

    /// <summary>Raw gateway payloads kept for auditing.</summary>
    public string? RawGatewayResponse { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}