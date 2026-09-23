using Backend.Common;
using Backend.DTOs.Owner;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/owner")]
[Authorize(Roles = "Owner")]
[Produces("application/json")]
public class OwnerController : ControllerBase
{
    private readonly IOwnerService _owner;

    public OwnerController(IOwnerService owner)
    {
        _owner = owner;
    }

    private int OwnerId => User.GetUserId();

    /* ---------------- Dashboard ---------------- */

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<OwnerDashboardDto>>> Dashboard(CancellationToken ct) =>
        Ok(ApiResponse<OwnerDashboardDto>.Ok(await _owner.GetDashboardAsync(OwnerId, ct)));

    /* ---------------- Profile ---------------- */

    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<FutsalProfileDto>>> GetProfile(CancellationToken ct) =>
        Ok(ApiResponse<FutsalProfileDto>.Ok(await _owner.GetProfileAsync(OwnerId, ct)));

    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<FutsalProfileDto>>> UpdateProfile(
        [FromBody] UpdateFutsalProfileDto request, CancellationToken ct) =>
        Ok(ApiResponse<FutsalProfileDto>.Ok(await _owner.UpdateProfileAsync(OwnerId, request, ct)));

    /* ---------------- Courts ---------------- */

    [HttpGet("courts")]
    public async Task<ActionResult<ApiResponse<List<CourtDto>>>> GetCourts(CancellationToken ct) =>
        Ok(ApiResponse<List<CourtDto>>.Ok(await _owner.GetCourtsAsync(OwnerId, ct)));

    [HttpPost("courts")]
    public async Task<ActionResult<ApiResponse<CourtDto>>> CreateCourt(
        [FromBody] SaveCourtDto request, CancellationToken ct)
    {
        var court = await _owner.CreateCourtAsync(OwnerId, request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<CourtDto>.Ok(court));
    }

    [HttpPut("courts/{id:int}")]
    public async Task<ActionResult<ApiResponse<CourtDto>>> UpdateCourt(
        int id, [FromBody] SaveCourtDto request, CancellationToken ct) =>
        Ok(ApiResponse<CourtDto>.Ok(await _owner.UpdateCourtAsync(OwnerId, id, request, ct)));

    [HttpDelete("courts/{id:int}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCourt(int id, CancellationToken ct)
    {
        await _owner.DeleteCourtAsync(OwnerId, id, ct);
        return Ok(ApiResponse<object>.Ok(new { deleted = true, id }));
    }

    /* ---------------- Bookings ---------------- */

    [HttpGet("bookings")]
    public async Task<ActionResult<ApiResponse<List<OwnerBookingDto>>>> GetBookings(
        [FromQuery] int? courtId,
        [FromQuery] DateOnly? date,
        [FromQuery] string? status,
        CancellationToken ct) =>
        Ok(ApiResponse<List<OwnerBookingDto>>.Ok(await _owner.GetBookingsAsync(OwnerId, courtId, date, status, ct)));

    [HttpPost("bookings")]
    public async Task<ActionResult<ApiResponse<OwnerBookingDto>>> CreateManualBooking(
        [FromBody] ManualBookingDto request, CancellationToken ct)
    {
        var booking = await _owner.CreateManualBookingAsync(OwnerId, request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<OwnerBookingDto>.Ok(booking));
    }

    [HttpPatch("bookings/{id:int}/mark-paid")]
    public async Task<ActionResult<ApiResponse<OwnerBookingDto>>> MarkPaid(int id, CancellationToken ct) =>
        Ok(ApiResponse<OwnerBookingDto>.Ok(await _owner.MarkPaidAsync(OwnerId, id, ct)));

    [HttpPatch("bookings/{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse<OwnerBookingDto>>> CancelBooking(
        int id, [FromBody] CancelBookingDto? request, CancellationToken ct) =>
        Ok(ApiResponse<OwnerBookingDto>.Ok(await _owner.CancelBookingAsync(OwnerId, id, request?.Reason, ct)));

    /* ---------------- Slot blocking ---------------- */

    [HttpPost("slots/block")]
    public async Task<ActionResult<ApiResponse<SlotBlockDto>>> BlockSlot(
        [FromBody] BlockSlotDto request, CancellationToken ct)
    {
        var block = await _owner.BlockSlotAsync(OwnerId, request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<SlotBlockDto>.Ok(block));
    }

    /* ---------------- Reviews ---------------- */

    [HttpGet("reviews")]
    public async Task<ActionResult<ApiResponse<List<OwnerReviewDto>>>> GetReviews(CancellationToken ct) =>
        Ok(ApiResponse<List<OwnerReviewDto>>.Ok(await _owner.GetReviewsAsync(OwnerId, ct)));

    [HttpPost("reviews/{id:int}/reply")]
    public async Task<ActionResult<ApiResponse<OwnerReviewDto>>> ReplyToReview(
        int id, [FromBody] ReviewReplyDto request, CancellationToken ct) =>
        Ok(ApiResponse<OwnerReviewDto>.Ok(await _owner.ReplyToReviewAsync(OwnerId, id, request.Text ?? string.Empty, ct)));
}