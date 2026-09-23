using Backend.DTOs.Admin;

namespace Backend.Services;

public interface IAdminService
{
    Task<AdminDashboardDto> GetDashboardAsync(CancellationToken ct = default);

    Task<List<OwnerListItemDto>> GetOwnersAsync(string? search, CancellationToken ct = default);
    Task<OwnerCredentialsDto> CreateOwnerAsync(CreateOwnerDto request, CancellationToken ct = default);
    Task<OwnerListItemDto> SetOwnerStatusAsync(int ownerId, bool isActive, CancellationToken ct = default);
    Task<OwnerCredentialsDto> ResetOwnerPasswordAsync(int ownerId, CancellationToken ct = default);

    Task<List<AdminFutsalListItemDto>> GetFutsalsAsync(CancellationToken ct = default);
    Task<AdminFutsalListItemDto> SetFutsalStatusAsync(int futsalId, bool isActive, CancellationToken ct = default);

    Task<List<AdminBookingListItemDto>> GetBookingsAsync(AdminBookingFilterDto filter, CancellationToken ct = default);
    Task<List<AdminTransactionDto>> GetTransactionsAsync(AdminTransactionFilterDto filter, CancellationToken ct = default);
}