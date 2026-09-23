using Backend.Common;
using Backend.DTOs.Futsal;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/futsals")]
[AllowAnonymous]
[Produces("application/json")]
public class FutsalsController : ControllerBase
{
    private readonly IFutsalService _futsals;

    public FutsalsController(IFutsalService futsals)
    {
        _futsals = futsals;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<FutsalListItemDto>>>> GetAll(
        [FromQuery] FutsalFilterDto filter, CancellationToken ct) =>
        Ok(ApiResponse<List<FutsalListItemDto>>.Ok(await _futsals.GetListAsync(filter, ct)));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<FutsalDetailDto>>> GetById(int id, CancellationToken ct) =>
        Ok(ApiResponse<FutsalDetailDto>.Ok(await _futsals.GetDetailAsync(id, ct)));

    [HttpGet("{id:int}/courts/{courtId:int}/availability")]
    public async Task<ActionResult<ApiResponse<AvailabilityDto>>> GetAvailability(
        int id, int courtId, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        var day = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(ApiResponse<AvailabilityDto>.Ok(await _futsals.GetAvailabilityAsync(id, courtId, day, ct)));
    }

    [HttpPost("{id:int}/reviews")]
    public async Task<ActionResult<ApiResponse<ReviewDto>>> AddReview(
        int id, [FromBody] CreateReviewDto request, CancellationToken ct)
    {
        var review = await _futsals.AddReviewAsync(id, request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<ReviewDto>.Ok(review));
    }
}