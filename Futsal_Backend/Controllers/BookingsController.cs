using Backend.Common;
using Backend.DTOs.Booking;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/bookings")]
[AllowAnonymous]
[Produces("application/json")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookings;

    public BookingsController(IBookingService bookings)
    {
        _bookings = bookings;
    }

    /// <summary>Cash returns the reference immediately; eSewa also returns the gateway form to submit.</summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<BookingResponseDto>>> Create(
        [FromBody] CreateBookingDto request, CancellationToken ct)
    {
        var booking = await _bookings.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<BookingResponseDto>.Ok(booking));
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<ApiResponse<BookingResponseDto>>> Lookup(
        [FromQuery] string referenceCode, [FromQuery] string contact, CancellationToken ct) =>
        Ok(ApiResponse<BookingResponseDto>.Ok(await _bookings.LookupAsync(referenceCode, contact, ct)));

    [HttpPatch("{referenceCode}/cancel")]
    public async Task<ActionResult<ApiResponse<BookingResponseDto>>> Cancel(
        string referenceCode, [FromQuery] string contact, CancellationToken ct) =>
        Ok(ApiResponse<BookingResponseDto>.Ok(await _bookings.CancelAsync(referenceCode, contact, ct)));
}