using Backend.Common;
using Backend.Data;
using Backend.DTOs.Booking;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public class BookingService : IBookingService
{
    private readonly ApplicationDbContext _db;
    private readonly ISlotAvailabilityService _slots;
    private readonly IEsewaPaymentService _esewa;
    private readonly IAppClock _clock;
    private readonly BookingOptions _options;

    public BookingService(
        ApplicationDbContext db,
        ISlotAvailabilityService slots,
        IEsewaPaymentService esewa,
        IAppClock clock,
        IOptions<BookingOptions> options)
    {
        _db = db;
        _slots = slots;
        _esewa = esewa;
        _clock = clock;
        _options = options.Value;
    }

    /* ==================== Create ==================== */

    public async Task<BookingResponseDto> CreateAsync(CreateBookingDto request, CancellationToken ct = default)
    {
        var name = InputRules.Clean(request.PlayerName)
            ?? throw AppException.Validation("playerName", "Please enter your name.");

        var contact = InputRules.NormalizePhone(request.Contact);
        if (!InputRules.IsValidNepalPhone(contact))
        {
            throw AppException.Validation("contactNumber", "Enter a valid 10-digit mobile number.");
        }

        var email = InputRules.Clean(request.EmailAddress);
        if (email is not null && !InputRules.IsValidEmail(email))
        {
            throw AppException.Validation("email", "Enter a valid email address.");
        }

        var method = DisplayText.ParsePaymentMethod(request.PaymentMethod);

        var check = await _slots.ValidateBookableAsync(
            request.CourtId, request.Date, request.StartTime, request.EndTime, allowPast: false, ct);

        if (!check.Court.Futsal.IsActive || !check.Court.Futsal.IsProfileComplete)
        {
            throw AppException.BadRequest("This venue is not taking bookings right now.", "futsal_unavailable");
        }

        if (request.FutsalId.HasValue && request.FutsalId.Value != check.Court.FutsalId)
        {
            throw AppException.BadRequest("That court does not belong to this venue.", "court_mismatch");
        }

        var booking = new Booking
        {
            CourtId = check.Court.Id,
            BookingDate = request.Date,
            StartTime = check.StartTime,
            EndTime = check.EndTime,
            ReferenceCode = await GenerateReferenceCodeAsync(ct),
            PlayerName = name,
            PlayerContact = contact,
            PlayerEmail = email,
            PaymentMethod = method,
            PaymentStatus = PaymentStatus.Pending,
            BookingStatus = BookingStatus.Confirmed,
            Source = BookingSource.PlayerOnline,
            TotalAmount = check.Amount,
        };

        // The partial unique index is the real guard against two players booking at once
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, out var constraint))
        {
            await transaction.RollbackAsync(ct);
            throw constraint == ApplicationDbContext.ReferenceCodeIndexName
                ? AppException.Conflict("Could not generate a booking reference. Please try again.")
                : AppException.Conflict(
                    "Sorry, that slot has just been taken. Please choose another time.", "slot_taken");
        }

        var response = await BuildResponseAsync(booking.Id, ct);

        if (method == PaymentMethod.Online)
        {
            response.Esewa = await _esewa.InitiateAsync(booking, ct);
        }

        return response;
    }

    /* ==================== Lookup ==================== */

    public async Task<BookingResponseDto> LookupAsync(
        string referenceCode, string contact, CancellationToken ct = default)
    {
        var booking = await FindAsync(referenceCode, contact, ct);
        return await BuildResponseAsync(booking.Id, ct);
    }

    public async Task<BookingResponseDto> GetByReferenceAsync(string referenceCode, CancellationToken ct = default)
    {
        var code = InputRules.Clean(referenceCode)?.ToUpperInvariant() ?? string.Empty;

        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.ReferenceCode == code, ct)
            ?? throw AppException.NotFound("We could not find that booking.", "booking_not_found");

        return await BuildResponseAsync(booking.Id, ct);
    }

    /* ==================== Cancel ==================== */

    public async Task<BookingResponseDto> CancelAsync(
        string referenceCode, string contact, CancellationToken ct = default)
    {
        var booking = await FindAsync(referenceCode, contact, ct);

        if (booking.BookingStatus == BookingStatus.Cancelled)
        {
            throw AppException.BadRequest("This booking is already cancelled.", "already_cancelled");
        }

        if (booking.BookingStatus is BookingStatus.Completed or BookingStatus.NoShow)
        {
            throw AppException.BadRequest("This booking can no longer be cancelled.", "not_cancellable");
        }

        var startsAtUtc = _clock.ToUtc(booking.BookingDate, booking.StartTime);
        var cutoff = DateTime.UtcNow.AddHours(_options.PlayerCancelCutoffHours);

        if (startsAtUtc <= cutoff)
        {
            throw AppException.BadRequest(
                $"Bookings can only be cancelled online up to {_options.PlayerCancelCutoffHours} hours before the start time. Please contact the venue.",
                "cancel_window_closed");
        }

        booking.BookingStatus = BookingStatus.Cancelled;
        booking.CancellationReason = "Cancelled by the player";
        booking.CancelledAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        if (booking.PaymentStatus == PaymentStatus.Paid)
        {
            booking.PaymentStatus = PaymentStatus.Refunded;
        }

        await _db.SaveChangesAsync(ct);
        return await BuildResponseAsync(booking.Id, ct);
    }

    /* ==================== Helpers ==================== */

    private async Task<Booking> FindAsync(string referenceCode, string contact, CancellationToken ct)
    {
        var code = InputRules.Clean(referenceCode)?.ToUpperInvariant()
            ?? throw AppException.Validation("referenceCode", "Enter your reference code.");

        var phone = InputRules.NormalizePhone(contact);
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw AppException.Validation("contact", "Enter the mobile number you booked with.");
        }

        return await _db.Bookings.FirstOrDefaultAsync(
                   b => b.ReferenceCode == code && b.PlayerContact == phone, ct)
               ?? throw AppException.NotFound(
                   "No booking matches that reference code and mobile number.", "booking_not_found");
    }

    private async Task<string> GenerateReferenceCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var code = CodeGenerator.ReferenceCode(_options.ReferencePrefix);
            if (!await _db.Bookings.AnyAsync(b => b.ReferenceCode == code, ct)) return code;
        }
        throw AppException.Conflict("Could not generate a booking reference. Please try again.");
    }

    private async Task<BookingResponseDto> BuildResponseAsync(int bookingId, CancellationToken ct)
    {
        var today = _clock.Today;
        var now = _clock.TimeOfDay;

        var dto = await _db.Bookings
            .Where(b => b.Id == bookingId)
            .Select(b => new BookingResponseDto
            {
                Id = b.Id,
                ReferenceCode = b.ReferenceCode,
                FutsalId = b.Court.FutsalId,
                FutsalName = b.Court.Futsal.Name,
                FutsalAddress = b.Court.Futsal.Address,
                FutsalContact = b.Court.Futsal.ContactNumber,
                CourtId = b.CourtId,
                CourtName = b.Court.Name,
                Date = b.BookingDate,
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                PlayerName = b.PlayerName,
                ContactNumber = b.PlayerContact,
                Email = b.PlayerEmail,
                Amount = b.TotalAmount,
                PaymentMethod = b.PaymentMethod == PaymentMethod.Online ? "Esewa" : "Cash",
                PaymentStatus = b.PaymentStatus.ToString(),
                Status = b.BookingStatus.ToString(),
                CanCancel = b.BookingStatus == BookingStatus.Confirmed &&
                            (b.BookingDate > today || (b.BookingDate == today && b.StartTime > now)),
                CanReview = b.BookingStatus != BookingStatus.Cancelled &&
                            b.Review == null &&
                            (b.BookingDate < today || (b.BookingDate == today && b.EndTime <= now)),
                CreatedAt = b.CreatedAt,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw AppException.NotFound("We could not find that booking.", "booking_not_found");

        return dto;
    }
}