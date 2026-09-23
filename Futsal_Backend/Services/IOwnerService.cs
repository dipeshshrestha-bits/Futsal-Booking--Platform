using Backend.DTOs.Owner;

namespace Backend.Services;

public interface IOwnerService
{
    Task<OwnerDashboardDto> GetDashboardAsync(int ownerId, CancellationToken ct = default);

    Task<FutsalProfileDto> GetProfileAsync(int ownerId, CancellationToken ct = default);
    Task<FutsalProfileDto> UpdateProfileAsync(int ownerId, UpdateFutsalProfileDto request, CancellationToken ct = default);

    Task<List<CourtDto>> GetCourtsAsync(int ownerId, CancellationToken ct = default);
    Task<CourtDto> CreateCourtAsync(int ownerId, SaveCourtDto request, CancellationToken ct = default);
    Task<CourtDto> UpdateCourtAsync(int ownerId, int courtId, SaveCourtDto request, CancellationToken ct = default);
    Task DeleteCourtAsync(int ownerId, int courtId, CancellationToken ct = default);

    Task<List<OwnerBookingDto>> GetBookingsAsync(
        int ownerId, int? courtId, DateOnly? date, string? status, CancellationToken ct = default);
    Task<OwnerBookingDto> CreateManualBookingAsync(int ownerId, ManualBookingDto request, CancellationToken ct = default);
    Task<OwnerBookingDto> MarkPaidAsync(int ownerId, int bookingId, CancellationToken ct = default);
    Task<OwnerBookingDto> CancelBookingAsync(int ownerId, int bookingId, string? reason, CancellationToken ct = default);

    Task<SlotBlockDto> BlockSlotAsync(int ownerId, BlockSlotDto request, CancellationToken ct = default);

    Task<List<OwnerReviewDto>> GetReviewsAsync(int ownerId, CancellationToken ct = default);
    Task<OwnerReviewDto> ReplyToReviewAsync(int ownerId, int reviewId, string replyText, CancellationToken ct = default);
}