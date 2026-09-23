using Backend.Common;
using Backend.Data;
using Backend.DTOs.Futsal;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public class SlotAvailabilityService : ISlotAvailabilityService
{
    private readonly ApplicationDbContext _db;
    private readonly IAppClock _clock;
    private readonly BookingOptions _bookingOptions;

    public SlotAvailabilityService(ApplicationDbContext db, IAppClock clock, IOptions<BookingOptions> bookingOptions)
    {
        _db = db;
        _clock = clock;
        _bookingOptions = bookingOptions.Value;
    }

    /* ==================== Availability ==================== */

    public async Task<AvailabilityDto> GetAvailabilityAsync(
        int courtId, DateOnly date, bool publicView, CancellationToken ct = default)
    {
        await ReleaseExpiredHoldsAsync(courtId, ct);

        var court = await _db.Courts
            .Include(c => c.Futsal)
            .FirstOrDefaultAsync(c => c.Id == courtId, ct)
            ?? throw AppException.NotFound("Court not found.");

        if (publicView && (!court.IsActive || !court.Futsal.IsActive))
        {
            throw AppException.NotFound("This court is not available for booking.");
        }

        var bookings = await _db.Bookings
            .Where(b => b.CourtId == courtId
                        && b.BookingDate == date
                        && b.BookingStatus != BookingStatus.Cancelled)
            .Select(b => new { b.StartTime, b.EndTime })
            .ToListAsync(ct);

        var blocks = await _db.SlotBlocks
            .Where(s => s.CourtId == courtId && s.Date == date)
            .Select(s => new { s.StartTime, s.EndTime, s.Reason })
            .ToListAsync(ct);

        var wholeDayBlock = blocks.FirstOrDefault(b => b.StartTime is null || b.EndTime is null);
        var isToday = date == _clock.Today;
        var now = _clock.TimeOfDay;

        var slots = new List<SlotDto>();
        foreach (var (start, end) in GenerateRanges(court))
        {
            var slot = new SlotDto
            {
                StartTime = start,
                EndTime = end,
                Price = CalculateAmount(court, start, end),
                Status = "Available",
                IsAvailable = true,
            };

            if (wholeDayBlock is not null)
            {
                slot.Status = "Blocked";
                slot.Reason = wholeDayBlock.Reason ?? "Closed for the day";
            }
            else if (blocks.Any(b => Overlaps(start, end, b.StartTime!.Value, b.EndTime!.Value)))
            {
                var block = blocks.First(b => Overlaps(start, end, b.StartTime!.Value, b.EndTime!.Value));
                slot.Status = "Blocked";
                slot.Reason = block.Reason ?? "Unavailable";
            }
            else if (bookings.Any(b => Overlaps(start, end, b.StartTime, b.EndTime)))
            {
                slot.Status = "Booked";
            }
            else if (date < _clock.Today || (isToday && start <= now))
            {
                slot.Status = "Past";
            }

            slot.IsAvailable = slot.Status == "Available";
            slots.Add(slot);
        }

        return new AvailabilityDto
        {
            FutsalId = court.FutsalId,
            CourtId = court.Id,
            CourtName = court.Name,
            Date = date,
            OpeningTime = court.OpeningTime,
            ClosingTime = court.ClosingTime,
            SlotDurationMinutes = court.SlotDurationMinutes,
            PricePerHour = court.PricePerHour,
            Slots = slots,
        };
    }

    /* ==================== Validation ==================== */

    public async Task<SlotCheckResult> ValidateBookableAsync(
        int courtId, DateOnly date, TimeOnly startTime, TimeOnly? endTime, bool allowPast, CancellationToken ct = default)
    {
        await ReleaseExpiredHoldsAsync(courtId, ct);

        var court = await _db.Courts
            .Include(c => c.Futsal)
            .FirstOrDefaultAsync(c => c.Id == courtId, ct)
            ?? throw AppException.NotFound("Court not found.");

        if (!court.IsActive)
        {
            throw AppException.BadRequest("This court is currently closed for booking.", "court_inactive");
        }

        var end = endTime ?? startTime.AddMinutes(court.SlotDurationMinutes);

        if (end <= startTime)
        {
            throw AppException.Validation("endTime", "End time must be after start time.");
        }

        if (startTime < court.OpeningTime || end > court.ClosingTime)
        {
            throw AppException.Validation(
                "startTime",
                $"This court is open from {court.OpeningTime:HH\\:mm} to {court.ClosingTime:HH\\:mm}.");
        }

        // Start must sit on the court's slot grid
        var minutesFromOpening = (int)(startTime - court.OpeningTime).TotalMinutes;
        if (minutesFromOpening % court.SlotDurationMinutes != 0)
        {
            throw AppException.Validation(
                "startTime", $"Start time must align with the court's {court.SlotDurationMinutes}-minute slots.");
        }

        if (!allowPast)
        {
            if (date < _clock.Today || (date == _clock.Today && startTime <= _clock.TimeOfDay))
            {
                throw AppException.BadRequest("That time has already passed.", "slot_past");
            }

            var maxDate = _clock.Today.AddDays(_bookingOptions.MaxAdvanceDays);
            if (date > maxDate)
            {
                throw AppException.BadRequest(
                    $"Bookings can be made up to {_bookingOptions.MaxAdvanceDays} days in advance.", "too_far_ahead");
            }
        }

        var blocked = await _db.SlotBlocks
            .Where(s => s.CourtId == courtId && s.Date == date)
            .Select(s => new { s.StartTime, s.EndTime })
            .ToListAsync(ct);

        if (blocked.Any(b => b.StartTime is null || b.EndTime is null ||
                             Overlaps(startTime, end, b.StartTime.Value, b.EndTime.Value)))
        {
            throw AppException.Conflict("That time is blocked for maintenance or a private event.", "slot_blocked");
        }

        var clash = await _db.Bookings
            .Where(b => b.CourtId == courtId
                        && b.BookingDate == date
                        && b.BookingStatus != BookingStatus.Cancelled
                        && b.StartTime < end
                        && startTime < b.EndTime)
            .AnyAsync(ct);

        if (clash)
        {
            throw AppException.Conflict("Sorry, that slot has just been taken. Please choose another time.", "slot_taken");
        }

        return new SlotCheckResult(court, startTime, end, CalculateAmount(court, startTime, end));
    }

    /* ==================== Expired online holds ==================== */

    public async Task ReleaseExpiredHoldsAsync(int? courtId = null, CancellationToken ct = default)
    {
        var minutes = _bookingOptions.OnlinePaymentHoldMinutes;
        if (minutes <= 0) return;

        var cutoff = DateTime.UtcNow.AddMinutes(-minutes);

        var query = _db.Bookings.Where(b =>
            b.PaymentMethod == PaymentMethod.Online &&
            b.PaymentStatus == PaymentStatus.Pending &&
            b.BookingStatus == BookingStatus.Confirmed &&
            b.CreatedAt < cutoff);

        if (courtId.HasValue) query = query.Where(b => b.CourtId == courtId.Value);

        var stale = await query.ToListAsync(ct);
        if (stale.Count == 0) return;

        foreach (var booking in stale)
        {
            booking.BookingStatus = BookingStatus.Cancelled;
            booking.PaymentStatus = PaymentStatus.Failed;
            booking.CancellationReason = "Online payment was not completed in time.";
            booking.CancelledAt = DateTime.UtcNow;
            booking.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    /* ==================== Helpers ==================== */

    public decimal CalculateAmount(Court court, TimeOnly startTime, TimeOnly endTime)
    {
        var minutes = (decimal)(endTime - startTime).TotalMinutes;
        return Math.Round(court.PricePerHour * minutes / 60m, 2, MidpointRounding.AwayFromZero);
    }

    private static IEnumerable<(TimeOnly Start, TimeOnly End)> GenerateRanges(Court court)
    {
        var duration = court.SlotDurationMinutes <= 0 ? 60 : court.SlotDurationMinutes;
        var cursor = court.OpeningTime;

        // Guard against a runaway loop if the data is odd
        for (var i = 0; i < 96; i++)
        {
            var end = cursor.AddMinutes(duration);
            if (end <= cursor || end > court.ClosingTime) yield break;

            yield return (cursor, end);
            cursor = end;
        }
    }

    private static bool Overlaps(TimeOnly startA, TimeOnly endA, TimeOnly startB, TimeOnly endB) =>
        startA < endB && startB < endA;
}