using Backend.Common;
using Backend.Data;
using Backend.DTOs.Futsal;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public class FutsalService : IFutsalService
{
    private readonly ApplicationDbContext _db;
    private readonly ISlotAvailabilityService _slots;
    private readonly IAppClock _clock;
    private readonly ReviewOptions _reviewOptions;

    public FutsalService(
        ApplicationDbContext db,
        ISlotAvailabilityService slots,
        IAppClock clock,
        IOptions<ReviewOptions> reviewOptions)
    {
        _db = db;
        _slots = slots;
        _clock = clock;
        _reviewOptions = reviewOptions.Value;
    }

    /// <summary>A venue is public only when it's active, complete, its owner is active, and it has an open court.</summary>
    private IQueryable<Models.Futsal> PublicFutsals() =>
        _db.Futsals.Where(f =>
            f.IsActive &&
            f.IsProfileComplete &&
            f.Owner.IsActive &&
            f.Courts.Any(c => c.IsActive));

    public async Task<List<FutsalListItemDto>> GetListAsync(FutsalFilterDto filter, CancellationToken ct = default)
    {
        var query = PublicFutsals();

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var city = filter.City.Trim();
            query = query.Where(f => f.City != null && EF.Functions.ILike(f.City, city));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{filter.Search.Trim()}%";
            query = query.Where(f =>
                EF.Functions.ILike(f.Name, term) ||
                (f.Address != null && EF.Functions.ILike(f.Address, term)) ||
                (f.City != null && EF.Functions.ILike(f.City, term)));
        }

        if (filter.MinPrice.HasValue)
        {
            query = query.Where(f => f.Courts.Any(c => c.IsActive && c.PricePerHour >= filter.MinPrice.Value));
        }

        if (filter.MaxPrice.HasValue)
        {
            query = query.Where(f => f.Courts.Any(c => c.IsActive && c.PricePerHour <= filter.MaxPrice.Value));
        }

        var items = await query
            .Select(f => new FutsalListItemDto
            {
                Id = f.Id,
                Name = f.Name,
                Description = f.Description,
                Address = f.Address,
                City = f.City,
                CoverImageUrl = f.CoverImageUrl,
                StartingPrice = f.Courts.Where(c => c.IsActive).Min(c => (decimal?)c.PricePerHour),
                AverageRating = f.Reviews.Any() ? Math.Round(f.Reviews.Average(r => (double)r.Rating), 2) : 0,
                ReviewCount = f.Reviews.Count,
                CourtCount = f.Courts.Count(c => c.IsActive),
                OpeningTime = f.OpeningTime,
                ClosingTime = f.ClosingTime,
            })
            .ToListAsync(ct);

        return (filter.Sort?.Trim().ToLowerInvariant()) switch
        {
            "price_asc" => items.OrderBy(f => f.StartingPrice ?? decimal.MaxValue).ToList(),
            "price_desc" => items.OrderByDescending(f => f.StartingPrice ?? 0).ToList(),
            "rating" => items.OrderByDescending(f => f.AverageRating).ThenByDescending(f => f.ReviewCount).ToList(),
            "name" => items.OrderBy(f => f.Name).ToList(),
            _ => items
                .OrderByDescending(f => f.AverageRating > 0)
                .ThenByDescending(f => f.AverageRating)
                .ThenBy(f => f.Name)
                .ToList(),
        };
    }

    public async Task<FutsalDetailDto> GetDetailAsync(int futsalId, CancellationToken ct = default) =>
        await PublicFutsals()
            .Where(f => f.Id == futsalId)
            .Select(f => new FutsalDetailDto
            {
                Id = f.Id,
                Name = f.Name,
                Description = f.Description,
                Address = f.Address,
                City = f.City,
                ContactNumber = f.ContactNumber,
                Email = f.Email,
                Latitude = f.Latitude,
                Longitude = f.Longitude,
                CoverImageUrl = f.CoverImageUrl,
                Images = f.Images.OrderBy(i => i.SortOrder).Select(i => i.ImageUrl).ToList(),
                Amenities = f.Amenities,
                OpeningTime = f.OpeningTime,
                ClosingTime = f.ClosingTime,
                StartingPrice = f.Courts.Where(c => c.IsActive).Min(c => (decimal?)c.PricePerHour),
                AverageRating = f.Reviews.Any() ? Math.Round(f.Reviews.Average(r => (double)r.Rating), 2) : 0,
                ReviewCount = f.Reviews.Count,
                CourtCount = f.Courts.Count(c => c.IsActive),
                Courts = f.Courts
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .Select(c => new PublicCourtDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        SurfaceType = c.SurfaceType,
                        OpeningTime = c.OpeningTime,
                        ClosingTime = c.ClosingTime,
                        SlotDurationMinutes = c.SlotDurationMinutes,
                        PricePerHour = c.PricePerHour,
                        IsActive = c.IsActive,
                    })
                    .ToList(),
                Reviews = f.Reviews
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(50)
                    .Select(r => new ReviewDto
                    {
                        Id = r.Id,
                        ReviewerName = r.PlayerName,
                        Rating = r.Rating,
                        Comment = r.Comment,
                        Reply = r.OwnerReply,
                        RepliedAt = r.OwnerReplyAt,
                        CreatedAt = r.CreatedAt,
                    })
                    .ToList(),
            })
            .FirstOrDefaultAsync(ct)
        ?? throw AppException.NotFound("This venue is not available.");

    public async Task<AvailabilityDto> GetAvailabilityAsync(
        int futsalId, int courtId, DateOnly date, CancellationToken ct = default)
    {
        var belongs = await PublicFutsals().AnyAsync(f => f.Id == futsalId && f.Courts.Any(c => c.Id == courtId), ct);
        if (!belongs)
        {
            throw AppException.NotFound("Court not found.");
        }

        return await _slots.GetAvailabilityAsync(courtId, date, publicView: true, ct);
    }

    public async Task<ReviewDto> AddReviewAsync(int futsalId, CreateReviewDto request, CancellationToken ct = default)
    {
        var futsal = await PublicFutsals().FirstOrDefaultAsync(f => f.Id == futsalId, ct)
            ?? throw AppException.NotFound("This venue is not available.");

        if (request.Rating is < 1 or > 5)
        {
            throw AppException.Validation("rating", "Choose a rating from 1 to 5.");
        }

        var comment = InputRules.Clean(request.Comment);
        var contact = InputRules.NormalizePhone(request.Contact);
        Booking? booking = null;
        var name = InputRules.Clean(request.Name);

        if (_reviewOptions.RequireVerifiedBooking)
        {
            var code = InputRules.Clean(request.ReferenceCode)?.ToUpperInvariant()
                ?? throw AppException.Validation("referenceCode", "Enter the reference code from your booking.");

            if (!InputRules.IsValidNepalPhone(contact))
            {
                throw AppException.Validation("contactNumber", "Enter the mobile number you booked with.");
            }

            booking = await _db.Bookings
                .Include(b => b.Review)
                .FirstOrDefaultAsync(b =>
                    b.ReferenceCode == code &&
                    b.PlayerContact == contact &&
                    b.Court.FutsalId == futsalId, ct)
                ?? throw AppException.NotFound(
                    "No booking at this venue matches that reference code and number.", "booking_not_found");

            if (booking.BookingStatus == BookingStatus.Cancelled)
            {
                throw AppException.BadRequest("You can't review a cancelled booking.", "booking_cancelled");
            }

            var hasPlayed = booking.BookingStatus == BookingStatus.Completed ||
                            booking.BookingDate < _clock.Today ||
                            (booking.BookingDate == _clock.Today && booking.EndTime <= _clock.TimeOfDay);

            if (!hasPlayed)
            {
                throw AppException.BadRequest("You can leave a review after your game.", "booking_not_played");
            }

            if (booking.Review is not null)
            {
                throw AppException.Conflict("You have already reviewed this booking.", "already_reviewed");
            }

            name ??= booking.PlayerName;
        }
        else if (string.IsNullOrWhiteSpace(name))
        {
            throw AppException.Validation("reviewerName", "Please enter your name.");
        }

        var review = new Review
        {
            FutsalId = futsal.Id,
            BookingId = booking?.Id,
            PlayerName = name ?? "Player",
            PlayerContact = contact,
            Rating = request.Rating,
            Comment = comment,
        };

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(ct);

        return new ReviewDto
        {
            Id = review.Id,
            ReviewerName = review.PlayerName,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt,
        };
    }
}