using Backend.Common;
using Backend.Data;
using Backend.DTOs.Admin;
using Backend.DTOs.Owner;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public class OwnerService : IOwnerService
{
    private readonly ApplicationDbContext _db;
    private readonly ISlotAvailabilityService _slots;
    private readonly IAppClock _clock;
    private readonly BookingOptions _bookingOptions;

    public OwnerService(
        ApplicationDbContext db,
        ISlotAvailabilityService slots,
        IAppClock clock,
        IOptions<BookingOptions> bookingOptions)
    {
        _db = db;
        _slots = slots;
        _clock = clock;
        _bookingOptions = bookingOptions.Value;
    }

    /* ==================== Scoping ==================== */

    /// <summary>Every action runs through this, so an owner can only ever touch their own venue.</summary>
    private async Task<Futsal> GetOwnFutsalAsync(int ownerId, CancellationToken ct) =>
        await _db.Futsals.FirstOrDefaultAsync(f => f.OwnerId == ownerId, ct)
        ?? throw AppException.NotFound("No venue is linked to your account. Please contact the platform admin.");

    private async Task<Court> GetOwnCourtAsync(int ownerId, int courtId, CancellationToken ct) =>
        await _db.Courts
            .Include(c => c.Futsal)
            .FirstOrDefaultAsync(c => c.Id == courtId && c.Futsal.OwnerId == ownerId, ct)
        ?? throw AppException.NotFound("Court not found.");

    /* ==================== Dashboard ==================== */

    public async Task<OwnerDashboardDto> GetDashboardAsync(int ownerId, CancellationToken ct = default)
    {
        await _slots.ReleaseExpiredHoldsAsync(ct: ct);

        var futsal = await GetOwnFutsalAsync(ownerId, ct);

        var today = _clock.Today;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var previousMonthStart = monthStart.AddMonths(-1);
        var seriesStart = monthStart.AddMonths(-5);

        var courts = await _db.Courts
            .Where(c => c.FutsalId == futsal.Id)
            .Select(c => new { c.Id, c.IsActive, c.OpeningTime, c.ClosingTime, c.SlotDurationMinutes })
            .ToListAsync(ct);

                var bookings = await QueryBookings(futsal.Id)
            .Where(b => b.Date >= seriesStart)
            .ToListAsync(ct);

        var live = bookings.Where(b => b.Status != BookingStatus.Cancelled.ToString()).ToList();
        var todays = live.Where(b => b.Date == today).OrderBy(b => b.StartTime).ToList();
        var thisMonth = live.Where(b => b.Date >= monthStart).ToList();
        var lastMonth = live.Where(b => b.Date >= previousMonthStart && b.Date < monthStart).ToList();

        var paid = (IEnumerable<OwnerBookingDto> source) =>
            source.Where(b => b.PaymentStatus == PaymentStatus.Paid.ToString()).Sum(b => b.Amount);

        var pendingCash = live
            .Where(b => b.PaymentStatus == PaymentStatus.Pending.ToString() && b.PaymentMethod == "Cash")
            .OrderBy(b => b.Date)
            .ThenBy(b => b.StartTime)
            .ToList();

        var growth = Enumerable.Range(0, 6)
            .Select(offset => seriesStart.AddMonths(offset))
            .Select(month =>
            {
                var next = month.AddMonths(1);
                var slice = live.Where(b => b.Date >= month && b.Date < next).ToList();
                return new GrowthPointDto
                {
                    Label = $"{month.Year:D4}-{month.Month:D2}",
                    Bookings = slice.Count,
                    Revenue = paid(slice),
                };
            })
            .ToList();

        // Occupancy: today's booked slots out of all slots the active courts offer today
        var totalSlotsToday = courts
            .Where(c => c.IsActive)
            .Sum(c =>
            {
                var duration = c.SlotDurationMinutes <= 0 ? 60 : c.SlotDurationMinutes;
                var minutes = (c.ClosingTime - c.OpeningTime).TotalMinutes;
                return minutes <= 0 ? 0 : (int)(minutes / duration);
            });

        var reviews = await _db.Reviews
            .Where(r => r.FutsalId == futsal.Id)
            .Select(r => new { r.Rating, r.OwnerReply })
            .ToListAsync(ct);

        return new OwnerDashboardDto
        {
            FutsalId = futsal.Id,
            FutsalName = futsal.Name,
            IsProfileComplete = futsal.IsProfileComplete,
            IsListedPublicly = futsal.IsActive && futsal.IsProfileComplete && courts.Any(c => c.IsActive),
            CourtCount = courts.Count,

            TodayBookingsCount = todays.Count,
            WeekBookings = live.Count(b => b.Date >= weekStart),
            MonthBookings = thisMonth.Count,

            TodayRevenue = paid(todays),
            WeekRevenue = paid(live.Where(b => b.Date >= weekStart)),
            MonthRevenue = paid(thisMonth),

            BookingsGrowth = PercentChange(thisMonth.Count, lastMonth.Count),
            RevenueGrowth = PercentChange(paid(thisMonth), paid(lastMonth)),

            OccupancyRate = totalSlotsToday > 0
                ? Math.Round(todays.Count * 100d / totalSlotsToday, 1)
                : 0d,

            PendingCashAmount = pendingCash.Sum(b => b.Amount),
            AverageRating = reviews.Count > 0 ? Math.Round(reviews.Average(r => (double)r.Rating), 2) : 0d,
            ReviewCount = reviews.Count,
            UnansweredReviews = reviews.Count(r => string.IsNullOrWhiteSpace(r.OwnerReply)),

            Growth = growth,
            TodaysBookings = todays,
            PendingCashPayments = pendingCash.Take(10).ToList(),
        };
    }

    private static double PercentChange(decimal current, decimal previous)
    {
        if (previous <= 0) return current > 0 ? 100d : 0d;
        return Math.Round((double)((current - previous) / previous) * 100d, 1);
    }

    /* ==================== Profile ==================== */

    public async Task<FutsalProfileDto> GetProfileAsync(int ownerId, CancellationToken ct = default)
    {
        await GetOwnFutsalAsync(ownerId, ct);
        return await BuildProfileAsync(ownerId, ct);
    }

    public async Task<FutsalProfileDto> UpdateProfileAsync(
        int ownerId, UpdateFutsalProfileDto request, CancellationToken ct = default)
    {
        var futsal = await _db.Futsals
            .Include(f => f.Images)
            .FirstOrDefaultAsync(f => f.OwnerId == ownerId, ct)
            ?? throw AppException.NotFound("No venue is linked to your account.");

        var contact = InputRules.NormalizePhone(request.ContactNumber);
        if (!string.IsNullOrWhiteSpace(contact) && !InputRules.IsValidNepalPhone(contact))
        {
            throw AppException.Validation("contactNumber", "Enter a valid 10-digit mobile number.");
        }

        var email = InputRules.Clean(request.Email);
        if (email is not null && !InputRules.IsValidEmail(email))
        {
            throw AppException.Validation("email", "Enter a valid email address.");
        }

        if (request.OpeningTime.HasValue && request.ClosingTime.HasValue &&
            request.ClosingTime.Value <= request.OpeningTime.Value)
        {
            throw AppException.Validation("closingTime", "Closing time must be after opening time.");
        }

        futsal.Name = request.Name.Trim();
        futsal.Description = InputRules.Clean(request.Description);
        futsal.Address = InputRules.Clean(request.Address);
        futsal.City = InputRules.Clean(request.City);
        futsal.Latitude = request.Latitude;
        futsal.Longitude = request.Longitude;
        futsal.ContactNumber = string.IsNullOrWhiteSpace(contact) ? null : contact;
        futsal.Email = email;
        futsal.OpeningTime = request.OpeningTime;
        futsal.ClosingTime = request.ClosingTime;
        futsal.UpdatedAt = DateTime.UtcNow;

        futsal.Amenities = (request.Amenities ?? new List<string>())
            .Select(a => a?.Trim() ?? string.Empty)
            .Where(a => a.Length is > 0 and <= 40)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        // Replace the gallery with what the owner submitted
        var images = (request.Images ?? new List<string>())
            .Select(url => url?.Trim() ?? string.Empty)
            .Where(url => url.Length is > 0 and <= 500)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();

        _db.FutsalImages.RemoveRange(futsal.Images);
        futsal.Images = images
            .Select((url, index) => new FutsalImage { FutsalId = futsal.Id, ImageUrl = url, SortOrder = index })
            .ToList();

        var cover = InputRules.Clean(request.CoverImageUrl);
        futsal.CoverImageUrl = cover ?? images.FirstOrDefault();

        futsal.IsProfileComplete =
            !string.IsNullOrWhiteSpace(futsal.Name) &&
            !string.IsNullOrWhiteSpace(futsal.Address) &&
            !string.IsNullOrWhiteSpace(futsal.City) &&
            !string.IsNullOrWhiteSpace(futsal.ContactNumber);

        await _db.SaveChangesAsync(ct);
        return await BuildProfileAsync(ownerId, ct);
    }

    private async Task<FutsalProfileDto> BuildProfileAsync(int ownerId, CancellationToken ct) =>
        await _db.Futsals
            .Where(f => f.OwnerId == ownerId)
            .Select(f => new FutsalProfileDto
            {
                Id = f.Id,
                Name = f.Name,
                Description = f.Description,
                Address = f.Address,
                City = f.City,
                Latitude = f.Latitude,
                Longitude = f.Longitude,
                ContactNumber = f.ContactNumber,
                Email = f.Email,
                OpeningTime = f.OpeningTime,
                ClosingTime = f.ClosingTime,
                Amenities = f.Amenities,
                CoverImageUrl = f.CoverImageUrl,
                Images = f.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).ToList(),
                IsActive = f.IsActive,
                IsProfileComplete = f.IsProfileComplete,
                IsListedPublicly = f.IsActive && f.IsProfileComplete && f.Courts.Any(c => c.IsActive),
                CourtCount = f.Courts.Count,
                AverageRating = f.Reviews.Any() ? Math.Round(f.Reviews.Average(r => (double)r.Rating), 2) : 0,
                ReviewCount = f.Reviews.Count,
            })
            .FirstAsync(ct);

    /* ==================== Courts ==================== */

    public async Task<List<CourtDto>> GetCourtsAsync(int ownerId, CancellationToken ct = default)
    {
        var futsal = await GetOwnFutsalAsync(ownerId, ct);
        var today = _clock.Today;

        return await _db.Courts
            .Where(c => c.FutsalId == futsal.Id)
            .OrderBy(c => c.Name)
            .Select(c => new CourtDto
            {
                Id = c.Id,
                FutsalId = c.FutsalId,
                Name = c.Name,
                SurfaceType = c.SurfaceType,
                OpeningTime = c.OpeningTime,
                ClosingTime = c.ClosingTime,
                SlotDurationMinutes = c.SlotDurationMinutes,
                PricePerHour = c.PricePerHour,
                IsActive = c.IsActive,
                UpcomingBookings = c.Bookings.Count(b =>
                    b.BookingStatus != BookingStatus.Cancelled && b.BookingDate >= today),
            })
            .ToListAsync(ct);
    }

    public async Task<CourtDto> CreateCourtAsync(int ownerId, SaveCourtDto request, CancellationToken ct = default)
    {
        var futsal = await GetOwnFutsalAsync(ownerId, ct);
        ValidateCourt(request);

        var name = request.Name.Trim();
        if (await _db.Courts.AnyAsync(c => c.FutsalId == futsal.Id && c.Name.ToLower() == name.ToLower(), ct))
        {
            throw AppException.Validation("name", "You already have a court with this name.");
        }

        var court = new Court
        {
            FutsalId = futsal.Id,
            Name = name,
            SurfaceType = InputRules.Clean(request.SurfaceType),
            OpeningTime = request.OpeningTime,
            ClosingTime = request.ClosingTime,
            SlotDurationMinutes = request.SlotDurationMinutes,
            PricePerHour = request.PricePerHour,
            IsActive = request.IsActive,
        };

        _db.Courts.Add(court);
        await _db.SaveChangesAsync(ct);

        return (await GetCourtsAsync(ownerId, ct)).First(c => c.Id == court.Id);
    }

    public async Task<CourtDto> UpdateCourtAsync(
        int ownerId, int courtId, SaveCourtDto request, CancellationToken ct = default)
    {
        var court = await GetOwnCourtAsync(ownerId, courtId, ct);
        ValidateCourt(request);

        var name = request.Name.Trim();
        if (await _db.Courts.AnyAsync(
                c => c.FutsalId == court.FutsalId && c.Id != courtId && c.Name.ToLower() == name.ToLower(), ct))
        {
            throw AppException.Validation("name", "You already have a court with this name.");
        }

        court.Name = name;
        court.SurfaceType = InputRules.Clean(request.SurfaceType);
        court.OpeningTime = request.OpeningTime;
        court.ClosingTime = request.ClosingTime;
        court.SlotDurationMinutes = request.SlotDurationMinutes;
        court.PricePerHour = request.PricePerHour;
        court.IsActive = request.IsActive;
        court.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return (await GetCourtsAsync(ownerId, ct)).First(c => c.Id == courtId);
    }

    public async Task DeleteCourtAsync(int ownerId, int courtId, CancellationToken ct = default)
    {
        var court = await GetOwnCourtAsync(ownerId, courtId, ct);

        var hasUpcoming = await _db.Bookings.AnyAsync(
            b => b.CourtId == courtId
                 && b.BookingStatus != BookingStatus.Cancelled
                 && b.BookingDate >= _clock.Today, ct);

        if (hasUpcoming)
        {
            throw AppException.Conflict(
                "This court has upcoming bookings. Turn off \"Open for booking\" instead of deleting it.",
                "court_has_bookings");
        }

        var hasHistory = await _db.Bookings.AnyAsync(b => b.CourtId == courtId, ct);
        if (hasHistory)
        {
            // Keep past bookings intact for reporting
            court.IsActive = false;
            court.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return;
        }

        _db.Courts.Remove(court);
        await _db.SaveChangesAsync(ct);
    }

    private static void ValidateCourt(SaveCourtDto request)
    {
        if (request.ClosingTime <= request.OpeningTime)
        {
            throw AppException.Validation("closingTime", "Closing time must be after opening time.");
        }

        var window = (request.ClosingTime - request.OpeningTime).TotalMinutes;
        if (window < request.SlotDurationMinutes)
        {
            throw AppException.Validation(
                "slotDurationMinutes", "Opening hours are shorter than one slot.");
        }
    }

    /* ==================== Bookings ==================== */

    private IQueryable<OwnerBookingDto> QueryBookings(int futsalId)
    {
        var today = _clock.Today;
        var now = _clock.TimeOfDay;

        return _db.Bookings
            .Where(b => b.Court.FutsalId == futsalId)
            .Select(b => new OwnerBookingDto
            {
                Id = b.Id,
                ReferenceCode = b.ReferenceCode,
                FutsalId = b.Court.FutsalId,
                FutsalName = b.Court.Futsal.Name,
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
                Source = b.Source.ToString(),
                Notes = b.Notes,
                CanCancel = b.BookingStatus == BookingStatus.Confirmed &&
                            (b.BookingDate > today || (b.BookingDate == today && b.StartTime > now)),
                CanMarkPaid = b.BookingStatus != BookingStatus.Cancelled && b.PaymentStatus != PaymentStatus.Paid,
                CreatedAt = b.CreatedAt,
            });
    }

    public async Task<List<OwnerBookingDto>> GetBookingsAsync(
        int ownerId, int? courtId, DateOnly? date, string? status, CancellationToken ct = default)
    {
        await _slots.ReleaseExpiredHoldsAsync(ct: ct);

        var futsal = await GetOwnFutsalAsync(ownerId, ct);
        var query = QueryBookings(futsal.Id);

        if (courtId.HasValue) query = query.Where(b => b.CourtId == courtId.Value);
        if (date.HasValue) query = query.Where(b => b.Date == date.Value);

        if (DisplayText.ParseEnumFilter<BookingStatus>(status) is { } parsed)
        {
            var text = parsed.ToString();
            query = query.Where(b => b.Status == text);
        }

        return await query
            .OrderByDescending(b => b.Date)
            .ThenBy(b => b.StartTime)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<OwnerBookingDto> CreateManualBookingAsync(
        int ownerId, ManualBookingDto request, CancellationToken ct = default)
    {
        var futsal = await GetOwnFutsalAsync(ownerId, ct);
        await GetOwnCourtAsync(ownerId, request.CourtId, ct);

        var contact = InputRules.NormalizePhone(request.Contact);
        if (!InputRules.IsValidNepalPhone(contact))
        {
            throw AppException.Validation("contactNumber", "Enter a valid 10-digit mobile number.");
        }

        var email = InputRules.Clean(request.Email);
        if (email is not null && !InputRules.IsValidEmail(email))
        {
            throw AppException.Validation("email", "Enter a valid email address.");
        }

        var source = DisplayText.ParseEnumFilter<BookingSource>(request.Source) ?? BookingSource.OwnerWalkIn;
        if (source == BookingSource.PlayerOnline) source = BookingSource.OwnerWalkIn;

        // Owners may record a booking that has already started (a walk-in mid-game)
        var check = await _slots.ValidateBookableAsync(
            request.CourtId, request.Date, request.StartTime, request.EndTime, allowPast: true, ct);

        var amount = request.Amount.HasValue && request.Amount.Value >= 0 ? request.Amount.Value : check.Amount;

        var booking = new Booking
        {
            CourtId = check.Court.Id,
            BookingDate = request.Date,
            StartTime = check.StartTime,
            EndTime = check.EndTime,
            ReferenceCode = await GenerateReferenceCodeAsync(ct),
            PlayerName = request.PlayerName.Trim(),
            PlayerContact = contact,
            PlayerEmail = email,
            PaymentMethod = PaymentMethod.Cash,
            PaymentStatus = request.IsPaid ? PaymentStatus.Paid : PaymentStatus.Pending,
            BookingStatus = BookingStatus.Confirmed,
            Source = source,
            TotalAmount = amount,
            Notes = InputRules.Clean(request.Note),
        };

        _db.Bookings.Add(booking);

        if (request.IsPaid)
        {
            _db.Payments.Add(new Payment
            {
                Booking = booking,
                Amount = amount,
                Method = TransactionMethod.Cash,
                Status = TransactionStatus.Success,
            });
        }

        await SaveBookingAsync(ct);

        return await QueryBookings(futsal.Id).FirstAsync(b => b.Id == booking.Id, ct);
    }

    public async Task<OwnerBookingDto> MarkPaidAsync(int ownerId, int bookingId, CancellationToken ct = default)
    {
        var booking = await GetOwnBookingAsync(ownerId, bookingId, ct);

        if (booking.BookingStatus == BookingStatus.Cancelled)
        {
            throw AppException.BadRequest("This booking was cancelled.", "booking_cancelled");
        }

        if (booking.PaymentStatus == PaymentStatus.Paid)
        {
            throw AppException.BadRequest("This booking is already marked as paid.", "already_paid");
        }

        booking.PaymentStatus = PaymentStatus.Paid;
        booking.UpdatedAt = DateTime.UtcNow;

        _db.Payments.Add(new Payment
        {
            BookingId = booking.Id,
            Amount = booking.TotalAmount,
            Method = booking.PaymentMethod == PaymentMethod.Online ? TransactionMethod.Esewa : TransactionMethod.Cash,
            Status = TransactionStatus.Success,
            RawGatewayResponse = "Marked paid by owner",
        });

        await _db.SaveChangesAsync(ct);
        return await QueryBookings(booking.Court.FutsalId).FirstAsync(b => b.Id == bookingId, ct);
    }

    public async Task<OwnerBookingDto> CancelBookingAsync(
        int ownerId, int bookingId, string? reason, CancellationToken ct = default)
    {
        var booking = await GetOwnBookingAsync(ownerId, bookingId, ct);

        if (booking.BookingStatus == BookingStatus.Cancelled)
        {
            throw AppException.BadRequest("This booking is already cancelled.", "already_cancelled");
        }

        if (booking.BookingStatus == BookingStatus.Completed)
        {
            throw AppException.BadRequest("A completed booking can't be cancelled.", "already_completed");
        }

        booking.BookingStatus = BookingStatus.Cancelled;
        booking.CancellationReason = InputRules.Clean(reason) ?? "Cancelled by the venue";
        booking.CancelledAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        if (booking.PaymentStatus == PaymentStatus.Paid)
        {
            booking.PaymentStatus = PaymentStatus.Refunded;
        }

        await _db.SaveChangesAsync(ct);
        return await QueryBookings(booking.Court.FutsalId).FirstAsync(b => b.Id == bookingId, ct);
    }

    private async Task<Booking> GetOwnBookingAsync(int ownerId, int bookingId, CancellationToken ct) =>
        await _db.Bookings
            .Include(b => b.Court)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.Court.Futsal.OwnerId == ownerId, ct)
        ?? throw AppException.NotFound("Booking not found.");

    private async Task<string> GenerateReferenceCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var code = CodeGenerator.ReferenceCode(_bookingOptions.ReferencePrefix);
            if (!await _db.Bookings.AnyAsync(b => b.ReferenceCode == code, ct)) return code;
        }
        throw AppException.Conflict("Could not generate a booking reference. Please try again.");
    }

    /// <summary>Turns the slot unique-index violation into a friendly conflict error.</summary>
    private async Task SaveBookingAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, out var constraint))
        {
            throw constraint == ApplicationDbContext.ReferenceCodeIndexName
                ? AppException.Conflict("Could not generate a booking reference. Please try again.")
                : AppException.Conflict("That slot has just been taken. Please choose another time.", "slot_taken");
        }
    }

    /* ==================== Slot blocks ==================== */

    public async Task<SlotBlockDto> BlockSlotAsync(int ownerId, BlockSlotDto request, CancellationToken ct = default)
    {
        var court = await GetOwnCourtAsync(ownerId, request.CourtId, ct);

        if (request.Date < _clock.Today)
        {
            throw AppException.Validation("date", "You can't block a date in the past.");
        }

        var hasStart = request.StartTime.HasValue;
        var hasEnd = request.EndTime.HasValue;
        if (hasStart != hasEnd)
        {
            throw AppException.Validation("endTime", "Provide both a start and an end time, or leave both empty to block the whole day.");
        }

        if (hasStart)
        {
            var start = request.StartTime!.Value;
            var end = request.EndTime!.Value;

            if (end <= start)
            {
                throw AppException.Validation("endTime", "End time must be after start time.");
            }

            if (start < court.OpeningTime || end > court.ClosingTime)
            {
                throw AppException.Validation(
                    "endTime",
                    $"Must be within court hours ({court.OpeningTime:HH\\:mm}–{court.ClosingTime:HH\\:mm}).");
            }

            var clash = await _db.Bookings.AnyAsync(
                b => b.CourtId == court.Id
                     && b.BookingDate == request.Date
                     && b.BookingStatus != BookingStatus.Cancelled
                     && b.StartTime < end
                     && start < b.EndTime, ct);

            if (clash)
            {
                throw AppException.Conflict(
                    "There is already a booking in that time. Cancel it first, then block the slot.", "slot_has_booking");
            }
        }
        else
        {
            var clash = await _db.Bookings.AnyAsync(
                b => b.CourtId == court.Id
                     && b.BookingDate == request.Date
                     && b.BookingStatus != BookingStatus.Cancelled, ct);

            if (clash)
            {
                throw AppException.Conflict(
                    "There are bookings on this date. Cancel them first, then block the day.", "day_has_bookings");
            }
        }

        var block = new SlotBlock
        {
            CourtId = court.Id,
            Date = request.Date,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Reason = InputRules.Clean(request.Reason) ?? "Maintenance",
        };

        _db.SlotBlocks.Add(block);
        await _db.SaveChangesAsync(ct);

        return new SlotBlockDto
        {
            Id = block.Id,
            CourtId = court.Id,
            CourtName = court.Name,
            Date = block.Date,
            StartTime = block.StartTime,
            EndTime = block.EndTime,
            Reason = block.Reason,
        };
    }

    /* ==================== Reviews ==================== */

    public async Task<List<OwnerReviewDto>> GetReviewsAsync(int ownerId, CancellationToken ct = default)
    {
        var futsal = await GetOwnFutsalAsync(ownerId, ct);

        return await _db.Reviews
            .Where(r => r.FutsalId == futsal.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new OwnerReviewDto
            {
                Id = r.Id,
                ReviewerName = r.PlayerName,
                Rating = r.Rating,
                Comment = r.Comment,
                Reply = r.OwnerReply,
                RepliedAt = r.OwnerReplyAt,
                CreatedAt = r.CreatedAt,
                BookingReference = r.Booking != null ? r.Booking.ReferenceCode : null,
            })
            .ToListAsync(ct);
    }

    public async Task<OwnerReviewDto> ReplyToReviewAsync(
        int ownerId, int reviewId, string replyText, CancellationToken ct = default)
    {
        var review = await _db.Reviews
            .Include(r => r.Booking)
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.Futsal.OwnerId == ownerId, ct)
            ?? throw AppException.NotFound("Review not found.");

        var text = InputRules.Clean(replyText)
            ?? throw AppException.Validation("reply", "Write a reply before posting.");

        if (text.Length > 500)
        {
            throw AppException.Validation("reply", "Reply must be 500 characters or less.");
        }

        review.OwnerReply = text;
        review.OwnerReplyAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new OwnerReviewDto
        {
            Id = review.Id,
            ReviewerName = review.PlayerName,
            Rating = review.Rating,
            Comment = review.Comment,
            Reply = review.OwnerReply,
            RepliedAt = review.OwnerReplyAt,
            CreatedAt = review.CreatedAt,
            BookingReference = review.Booking?.ReferenceCode,
        };
    }
}