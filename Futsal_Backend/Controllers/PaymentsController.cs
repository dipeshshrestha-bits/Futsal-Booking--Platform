using Backend.Common;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Backend.Controllers;

/// <summary>eSewa redirects the browser here, so these actions redirect back to the frontend.</summary>
[ApiController]
[Route("api/payments")]
[AllowAnonymous]
public class PaymentsController : ControllerBase
{
    private readonly IEsewaPaymentService _esewa;
    private readonly ILogger<PaymentsController> _logger;
    private readonly AppOptions _app;

    public PaymentsController(
        IEsewaPaymentService esewa,
        ILogger<PaymentsController> logger,
        IOptions<AppOptions> app)
    {
        _esewa = esewa;
        _logger = logger;
        _app = app.Value;
    }

    [HttpGet("esewa/success")]
    public async Task<IActionResult> EsewaSuccess([FromQuery] string? data, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return Redirect(FrontendUrl(null, success: false));
        }

        try
        {
            var result = await _esewa.HandleSuccessAsync(data, ct);
            return Redirect(FrontendUrl(result.ReferenceCode, result.Succeeded));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "eSewa success callback failed.");
            return Redirect(FrontendUrl(null, success: false));
        }
    }

    [HttpGet("esewa/failure")]
    public async Task<IActionResult> EsewaFailure([FromQuery] string? data, CancellationToken ct)
    {
        try
        {
            var result = await _esewa.HandleFailureAsync(data, ct);
            return Redirect(FrontendUrl(result.ReferenceCode, success: false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "eSewa failure callback failed.");
            return Redirect(FrontendUrl(null, success: false));
        }
    }

    /// <summary>Sends the player back to the confirmation page the React app renders.</summary>
    private string FrontendUrl(string? referenceCode, bool success)
    {
        var baseUrl = _app.FrontendBaseUrl.TrimEnd('/');
        var status = success ? "success" : "failure";

        return string.IsNullOrWhiteSpace(referenceCode)
            ? $"{baseUrl}/my-booking?payment={status}"
            : $"{baseUrl}/booking/confirmation/{Uri.EscapeDataString(referenceCode)}?payment={status}";
    }
}