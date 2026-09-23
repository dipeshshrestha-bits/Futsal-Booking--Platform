using System.Security.Claims;

namespace Backend.Common;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Reads the user id from the JWT "sub" claim.</summary>
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst("sub")?.Value;
        return int.TryParse(value, out var id)
            ? id
            : throw AppException.Unauthorized("Your session is invalid. Please sign in again.");
    }
}