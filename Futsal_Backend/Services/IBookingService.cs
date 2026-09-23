using Backend.DTOs.Booking;

namespace Backend.Services;

public interface IBookingService
{
    Task<BookingResponseDto> CreateAsync(CreateBookingDto request, CancellationToken ct = default);
    Task<BookingResponseDto> LookupAsync(string referenceCode, string contact, CancellationToken ct = default);
    Task<BookingResponseDto> CancelAsync(string referenceCode, string contact, CancellationToken ct = default);
    Task<BookingResponseDto> GetByReferenceAsync(string referenceCode, CancellationToken ct = default);
}