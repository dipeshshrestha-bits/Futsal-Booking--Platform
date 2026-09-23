using Backend.Common;
using Backend.DTOs.Admin;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminController(IAdminService admin)
    {
        _admin = admin;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<AdminDashboardDto>>> Dashboard(CancellationToken ct) =>
        Ok(ApiResponse<AdminDashboardDto>.Ok(await _admin.GetDashboardAsync(ct)));

    /* ---------------- Owners ---------------- */

    [HttpGet("owners")]
    public async Task<ActionResult<ApiResponse<List<OwnerListItemDto>>>> Owners(
        [FromQuery] string? search, CancellationToken ct) =>
        Ok(ApiResponse<List<OwnerListItemDto>>.Ok(await _admin.GetOwnersAsync(search, ct)));

    /// <summary>Creates the Owner account and its (empty) futsal listing, and returns the temporary password once.</summary>
    [HttpPost("owners")]
    public async Task<ActionResult<ApiResponse<OwnerCredentialsDto>>> CreateOwner(
        [FromBody] CreateOwnerDto request, CancellationToken ct)
    {
        var result = await _admin.CreateOwnerAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<OwnerCredentialsDto>.Ok(result));
    }

    [HttpPatch("owners/{id:int}/status")]
    public async Task<ActionResult<ApiResponse<OwnerListItemDto>>> SetOwnerStatus(
        int id, [FromBody] StatusUpdateDto request, CancellationToken ct) =>
        Ok(ApiResponse<OwnerListItemDto>.Ok(await _admin.SetOwnerStatusAsync(id, request.IsActive, ct)));

    [HttpPost("owners/{id:int}/reset-password")]
    public async Task<ActionResult<ApiResponse<OwnerCredentialsDto>>> ResetOwnerPassword(
        int id, CancellationToken ct) =>
        Ok(ApiResponse<OwnerCredentialsDto>.Ok(await _admin.ResetOwnerPasswordAsync(id, ct)));

    /* ---------------- Futsals ---------------- */

    [HttpGet("futsals")]
    public async Task<ActionResult<ApiResponse<List<AdminFutsalListItemDto>>>> Futsals(CancellationToken ct) =>
        Ok(ApiResponse<List<AdminFutsalListItemDto>>.Ok(await _admin.GetFutsalsAsync(ct)));

    [HttpPatch("futsals/{id:int}/status")]
    public async Task<ActionResult<ApiResponse<AdminFutsalListItemDto>>> SetFutsalStatus(
        int id, [FromBody] StatusUpdateDto request, CancellationToken ct) =>
        Ok(ApiResponse<AdminFutsalListItemDto>.Ok(await _admin.SetFutsalStatusAsync(id, request.IsActive, ct)));

    /* ---------------- Records ---------------- */

    [HttpGet("bookings")]
    public async Task<ActionResult<ApiResponse<List<AdminBookingListItemDto>>>> Bookings(
        [FromQuery] AdminBookingFilterDto filter, CancellationToken ct) =>
        Ok(ApiResponse<List<AdminBookingListItemDto>>.Ok(await _admin.GetBookingsAsync(filter, ct)));

    [HttpGet("transactions")]
    public async Task<ActionResult<ApiResponse<List<AdminTransactionDto>>>> Transactions(
        [FromQuery] AdminTransactionFilterDto filter, CancellationToken ct) =>
        Ok(ApiResponse<List<AdminTransactionDto>>.Ok(await _admin.GetTransactionsAsync(filter, ct)));
}