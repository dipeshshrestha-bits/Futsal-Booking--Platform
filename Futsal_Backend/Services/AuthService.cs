using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Backend.Common;
using Backend.Data;
using Backend.DTOs.Auth;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Services;

public class AuthService : IAuthService
{
    private const int BcryptWorkFactor = 12;

    private readonly ApplicationDbContext _db;
    private readonly JwtOptions _jwt;

    public AuthService(ApplicationDbContext db, IOptions<JwtOptions> jwt)
    {
        _db = db;
        _jwt = jwt.Value;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, UserRole expectedRole, CancellationToken ct = default)
    {
        var identifier = request.Identifier?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw AppException.Validation("email", "Enter your email or username.");
        }

        var user = await _db.Users
            .Include(u => u.Futsal)
            .FirstOrDefaultAsync(u => u.Username == identifier || u.Email == identifier, ct);

        // Same message whether the account is missing or the password is wrong
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw AppException.Unauthorized("Incorrect email or password.", "invalid_credentials");
        }

        // The security rule: correct credentials still fail on the wrong login endpoint
        if (user.Role != expectedRole)
        {
            throw AppException.Unauthorized("This account cannot sign in here.", "invalid_role");
        }

        if (!user.IsActive)
        {
            throw AppException.Forbidden(
                "This account has been deactivated. Please contact the platform admin.", "account_inactive");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return BuildResponse(user);
    }

    public async Task<LoginResponseDto> ChangeOwnerPasswordAsync(int userId, ChangePasswordDto request, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.Futsal)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw AppException.Unauthorized("Your session is invalid. Please sign in again.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw AppException.Validation("currentPassword", "Your current password is incorrect.");
        }

        if (!InputRules.IsStrongPassword(request.NewPassword))
        {
            throw AppException.Validation(
                "newPassword", "New password must be at least 8 characters and include a letter and a number.");
        }

        if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
        {
            throw AppException.Validation("newPassword", "New password must be different from the current one.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, BcryptWorkFactor);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Fresh token so the session reflects the cleared flag
        return BuildResponse(user);
    }

    private LoginResponseDto BuildResponse(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.ExpiryMinutes <= 0 ? 480 : _jwt.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("role", user.Role.ToString()),
            new("username", user.Username),
            new("email", user.Email),
            new("name", user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        if (user.Futsal is not null)
        {
            claims.Add(new Claim("futsalId", user.Futsal.Id.ToString()));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return new LoginResponseDto
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Role = user.Role.ToString(),
            MustChangePassword = user.MustChangePassword,
            ExpiresAt = expiresAt,
            User = new AuthUserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Username = user.Username,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role.ToString(),
                FutsalId = user.Futsal?.Id,
                FutsalName = user.Futsal?.Name,
                IsProfileComplete = user.Futsal?.IsProfileComplete,
            },
        };
    }
}