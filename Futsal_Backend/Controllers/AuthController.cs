using Backend.Common;
using Backend.DTOs.Auth;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    /// <summary>Admin sign-in. Owner credentials are rejected here.</summary>
    [HttpPost("admin/login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> AdminLogin(
        [FromBody] LoginRequestDto request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, UserRole.Admin, ct);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }

    /// <summary>Owner sign-in. Admin credentials are rejected here.</summary>
    [HttpPost("owner/login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> OwnerLogin(
        [FromBody] LoginRequestDto request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, UserRole.Owner, ct);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }

    /// <summary>Clears MustChangePassword and returns a fresh token.</summary>
    [HttpPost("owner/change-password")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> ChangePassword(
        [FromBody] ChangePasswordDto request, CancellationToken ct)
    {
        var result = await _auth.ChangeOwnerPasswordAsync(User.GetUserId(), request, ct);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }
}