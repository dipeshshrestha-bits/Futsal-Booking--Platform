using Backend.DTOs.Futsal;
using Backend.Models;

namespace Backend.Services;

/// <summary>Result of checking one requested time range.</summary>
public record SlotCheckResult(Court Court, TimeOnly StartTime, TimeOnly EndTime, decimal Amount);

public interface ISlotAvailabilityService
{
    /// <summary>Generates the slot list for a court and date, marking each Available, Booked, Blocked or Past.</summary>
    Task<AvailabilityDto> GetAvailabilityAsync(int courtId, DateOnly date, bool publicView, CancellationToken ct = default);

    /// <summary>
    /// Validates a requested range (court hours, alignment, past times, blocks, clashes)
    /// and returns the price. Throws AppException when the slot can't be booked.
    /// </summary>
    Task<SlotCheckResult> ValidateBookableAsync(
        int courtId, DateOnly date, TimeOnly startTime, TimeOnly? endTime, bool allowPast, CancellationToken ct = default);

    /// <summary>Cancels unpaid online bookings whose payment window has expired, freeing their slots.</summary>
    Task ReleaseExpiredHoldsAsync(int? courtId = null, CancellationToken ct = default);

    decimal CalculateAmount(Court court, TimeOnly startTime, TimeOnly endTime);
}