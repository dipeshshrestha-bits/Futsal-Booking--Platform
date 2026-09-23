using Backend.DTOs.Futsal;

namespace Backend.Services;

public interface IFutsalService
{
    Task<List<FutsalListItemDto>> GetListAsync(FutsalFilterDto filter, CancellationToken ct = default);
    Task<FutsalDetailDto> GetDetailAsync(int futsalId, CancellationToken ct = default);
    Task<AvailabilityDto> GetAvailabilityAsync(int futsalId, int courtId, DateOnly date, CancellationToken ct = default);
    Task<ReviewDto> AddReviewAsync(int futsalId, CreateReviewDto request, CancellationToken ct = default);
}