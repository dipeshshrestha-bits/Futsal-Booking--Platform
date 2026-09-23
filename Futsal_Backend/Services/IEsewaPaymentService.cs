using Backend.DTOs.Booking;
using Backend.Models;

namespace Backend.Services;

public record EsewaCallbackResult(string ReferenceCode, bool Succeeded, string Message);

public interface IEsewaPaymentService
{
    /// <summary>Creates the Payment row and builds the signed form the browser posts to eSewa.</summary>
    Task<EsewaFormDto> InitiateAsync(Booking booking, CancellationToken ct = default);

    /// <summary>Handles the gateway's success redirect: verify signature, confirm via status check, then mark Paid.</summary>
    Task<EsewaCallbackResult> HandleSuccessAsync(string encodedData, CancellationToken ct = default);

    /// <summary>Handles the failure redirect: mark the attempt failed and release the slot.</summary>
    Task<EsewaCallbackResult> HandleFailureAsync(string? encodedData, CancellationToken ct = default);
}