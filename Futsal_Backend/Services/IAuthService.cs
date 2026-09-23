using Backend.DTOs.Auth;
using Backend.Models;

namespace Backend.Services;

public interface IAuthService
{
    /// <summary>Signs in and rejects the attempt when the account's role doesn't match this endpoint.</summary>
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, UserRole expectedRole, CancellationToken ct = default);

    Task<LoginResponseDto> ChangeOwnerPasswordAsync(int userId, ChangePasswordDto request, CancellationToken ct = default);
}