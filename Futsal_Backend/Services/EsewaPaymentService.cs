using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Backend.Common;
using Backend.Data;
using Backend.DTOs.Booking;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public class EsewaPaymentService : IEsewaPaymentService
{
    /// <summary>Fields we sign on the way out. eSewa signs a different set on the way back.</summary>
    private const string RequestSignedFieldNames = "total_amount,transaction_uuid,product_code";

    /// <summary>Statuses that mean "not settled yet": keep the booking, don't cancel it.</summary>
    private static readonly string[] InconclusiveStatuses =
        { "UNREACHABLE", "UNKNOWN", "PENDING", "AMBIGUOUS" };

    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EsewaPaymentService> _logger;
    private readonly EsewaOptions _esewa;
    private readonly AppOptions _app;

    public EsewaPaymentService(
        ApplicationDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<EsewaPaymentService> logger,
        IOptions<EsewaOptions> esewa,
        IOptions<AppOptions> app)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _esewa = esewa.Value;
        _app = app.Value;
    }

    /* ==================== Initiate ==================== */

    public async Task<EsewaFormDto> InitiateAsync(Booking booking, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_esewa.ProductCode) ||
            string.IsNullOrWhiteSpace(_esewa.SecretKey) ||
            string.IsNullOrWhiteSpace(_esewa.FormUrl))
        {
            throw AppException.BadRequest(
                "Online payment is not configured. Please choose cash on arrival.", "esewa_not_configured");
        }

        // A fresh uuid per attempt keeps retries separate at the gateway
        var transactionUuid = $"{booking.ReferenceCode}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        _db.Payments.Add(new Payment
        {
            BookingId = booking.Id,
            Amount = booking.TotalAmount,
            Method = TransactionMethod.Esewa,
            Status = TransactionStatus.Initiated,
            GatewayTransactionUuid = transactionUuid,
        });
        await _db.SaveChangesAsync(ct);

        var totalAmount = FormatAmount(booking.TotalAmount);
        var apiBase = _app.PublicApiBaseUrl.TrimEnd('/');

        var fields = new Dictionary<string, string>
        {
            ["amount"] = totalAmount,
            ["tax_amount"] = "0",
            ["total_amount"] = totalAmount,
            ["transaction_uuid"] = transactionUuid,
            ["product_code"] = _esewa.ProductCode,
            ["product_service_charge"] = "0",
            ["product_delivery_charge"] = "0",
            ["success_url"] = $"{apiBase}/api/payments/esewa/success",
            ["failure_url"] = $"{apiBase}/api/payments/esewa/failure",
            ["signed_field_names"] = RequestSignedFieldNames,
            ["signature"] = ComputeRequestSignature(totalAmount, transactionUuid, _esewa.ProductCode),
        };

        return new EsewaFormDto { FormUrl = _esewa.FormUrl, Fields = fields };
    }

    /* ==================== Success callback ==================== */

    public async Task<EsewaCallbackResult> HandleSuccessAsync(string encodedData, CancellationToken ct = default)
    {
        var payload = DecodePayload(encodedData);
        var transactionUuid = payload.GetString("transaction_uuid")
            ?? throw AppException.BadRequest("The payment response is missing its transaction id.", "invalid_callback");

        var payment = await FindPaymentAsync(transactionUuid, ct);
        var booking = payment.Booking;

        payment.RawGatewayResponse = payload.Raw;
        payment.GatewayRefId = payload.GetString("transaction_code") ?? payload.GetString("ref_id");

        // Already confirmed: eSewa can redirect twice
        if (payment.Status == TransactionStatus.Success && booking.PaymentStatus == PaymentStatus.Paid)
        {
            await _db.SaveChangesAsync(ct);
            return new EsewaCallbackResult(booking.ReferenceCode, true, "Payment already confirmed.");
        }

        var productCode = payload.GetString("product_code") ?? _esewa.ProductCode;

        // 1. Verify the redirect signature over the fields eSewa itself signed
        if (!VerifyResponseSignature(payload))
        {
            _logger.LogWarning(
                "eSewa response signature did not match for {Uuid}; relying on the status check instead.",
                transactionUuid);
        }

        // 2. The status check is authoritative: a forged redirect can't fake this
        var status = await CheckStatusAsync(productCode, transactionUuid, booking.TotalAmount, ct);
        payment.RawGatewayResponse = $"{payload.Raw}\n--- status check ---\n{status.Raw}";

        if (string.Equals(status.Status, "COMPLETE", StringComparison.OrdinalIgnoreCase))
        {
            payment.Status = TransactionStatus.Success;
            payment.GatewayRefId ??= status.RefId;
            payment.UpdatedAt = DateTime.UtcNow;

            booking.PaymentStatus = PaymentStatus.Paid;
            booking.BookingStatus = BookingStatus.Confirmed;
            booking.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return new EsewaCallbackResult(booking.ReferenceCode, true, "Payment confirmed.");
        }

        // Couldn't reach eSewa, or it's still settling: keep the booking and let the hold window decide
        if (InconclusiveStatuses.Contains(status.Status, StringComparer.OrdinalIgnoreCase))
        {
            payment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogWarning(
                "eSewa status for {Uuid} was {Status}; leaving the booking pending.", transactionUuid, status.Status);

            return new EsewaCallbackResult(booking.ReferenceCode, false, "We're still confirming your payment.");
        }

        _logger.LogWarning("eSewa status for {Uuid} was {Status}", transactionUuid, status.Status);
        return await FailAsync(payment, booking, $"eSewa reported the payment as {status.Status}.", ct);
    }

    /* ==================== Failure callback ==================== */

    public async Task<EsewaCallbackResult> HandleFailureAsync(string? encodedData, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(encodedData))
        {
            return new EsewaCallbackResult(string.Empty, false, "Payment was cancelled.");
        }

        var payload = DecodePayload(encodedData);
        var transactionUuid = payload.GetString("transaction_uuid");
        if (string.IsNullOrWhiteSpace(transactionUuid))
        {
            return new EsewaCallbackResult(string.Empty, false, "Payment was cancelled.");
        }

        var payment = await FindPaymentAsync(transactionUuid, ct);
        payment.RawGatewayResponse = payload.Raw;

        // The player may have abandoned the gateway and paid on a later attempt
        var status = await CheckStatusAsync(
            payload.GetString("product_code") ?? _esewa.ProductCode,
            transactionUuid,
            payment.Booking.TotalAmount,
            ct);

        if (string.Equals(status.Status, "COMPLETE", StringComparison.OrdinalIgnoreCase))
        {
            payment.Status = TransactionStatus.Success;
            payment.GatewayRefId ??= status.RefId;
            payment.UpdatedAt = DateTime.UtcNow;

            payment.Booking.PaymentStatus = PaymentStatus.Paid;
            payment.Booking.BookingStatus = BookingStatus.Confirmed;
            payment.Booking.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            return new EsewaCallbackResult(payment.Booking.ReferenceCode, true, "Payment confirmed.");
        }

        return await FailAsync(payment, payment.Booking, "Payment was not completed.", ct);
    }

    private async Task<EsewaCallbackResult> FailAsync(
        Payment payment, Booking booking, string message, CancellationToken ct)
    {
        payment.Status = TransactionStatus.Failed;
        payment.UpdatedAt = DateTime.UtcNow;

        // Release the slot unless the payment already succeeded another way
        if (booking.PaymentStatus != PaymentStatus.Paid)
        {
            booking.PaymentStatus = PaymentStatus.Failed;
            booking.BookingStatus = BookingStatus.Cancelled;
            booking.CancellationReason = "Online payment was not completed.";
            booking.CancelledAt = DateTime.UtcNow;
            booking.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return new EsewaCallbackResult(booking.ReferenceCode, false, message);
    }

    private async Task<Payment> FindPaymentAsync(string transactionUuid, CancellationToken ct) =>
        await _db.Payments
            .Include(p => p.Booking)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(p => p.GatewayTransactionUuid == transactionUuid, ct)
        ?? throw AppException.NotFound("We could not find that payment.", "payment_not_found");

    /* ==================== Status check ==================== */

    private record StatusCheckResult(string Status, string? RefId, string Raw);

    private async Task<StatusCheckResult> CheckStatusAsync(
        string productCode, string transactionUuid, decimal amount, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_esewa.StatusCheckUrl))
        {
            return new StatusCheckResult("UNKNOWN", null, "Status check URL is not configured.");
        }

        var url = $"{_esewa.StatusCheckUrl.TrimEnd('/')}/" +
                  $"?product_code={Uri.EscapeDataString(productCode)}" +
                  $"&total_amount={Uri.EscapeDataString(FormatAmount(amount))}" +
                  $"&transaction_uuid={Uri.EscapeDataString(transactionUuid)}";

        try
        {
            var client = _httpClientFactory.CreateClient("esewa");
            using var response = await client.GetAsync(url, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "eSewa status check returned HTTP {Code} for {Uuid}: {Body}",
                    (int)response.StatusCode, transactionUuid, body);
                return new StatusCheckResult("UNREACHABLE", null, $"HTTP {(int)response.StatusCode}: {body}");
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            var status = root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString() ?? "UNKNOWN"
                : "UNKNOWN";

            var refId = root.TryGetProperty("ref_id", out var refElement) ? refElement.GetString() : null;

            _logger.LogInformation("eSewa status check for {Uuid}: {Status}", transactionUuid, status);
            return new StatusCheckResult(status, refId, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "eSewa status check failed for {Uuid}", transactionUuid);
            return new StatusCheckResult("UNREACHABLE", null, ex.Message);
        }
    }

    /* ==================== Signatures ==================== */

    /// <summary>Outgoing: HMAC-SHA256 over "total_amount=X,transaction_uuid=Y,product_code=Z", Base64 encoded.</summary>
    private string ComputeRequestSignature(string totalAmount, string transactionUuid, string productCode) =>
        Sign($"total_amount={totalAmount},transaction_uuid={transactionUuid},product_code={productCode}");

    /// <summary>
    /// Incoming: eSewa signs the fields its own response names in signed_field_names
    /// (transaction_code, status, total_amount, transaction_uuid, product_code, signed_field_names),
    /// using the values exactly as sent — including amounts like "1,500.0".
    /// </summary>
    private bool VerifyResponseSignature(GatewayPayload payload)
    {
        var signedFieldNames = payload.GetString("signed_field_names");
        var received = payload.GetString("signature");

        if (string.IsNullOrWhiteSpace(signedFieldNames) || string.IsNullOrWhiteSpace(received))
        {
            return false;
        }

        var message = string.Join(",", signedFieldNames
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(field => field.Trim())
            .Select(field => $"{field}={payload.GetString(field) ?? string.Empty}"));

        return FixedTimeEquals(Sign(message), received);
    }

    private string Sign(string message)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_esewa.SecretKey));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(message)));
    }

    private static bool FixedTimeEquals(string expected, string? received)
    {
        if (string.IsNullOrWhiteSpace(received)) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(received.Trim()));
    }

    /* ==================== Helpers ==================== */

    private static string FormatAmount(decimal amount) =>
        amount == Math.Floor(amount)
            ? ((long)amount).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private record GatewayPayload(JsonElement Root, string Raw)
    {
        public string? GetString(string name) =>
            Root.ValueKind == JsonValueKind.Object && Root.TryGetProperty(name, out var element)
                ? element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => element.GetRawText(),
                    _ => null,
                }
                : null;
    }

    private static GatewayPayload DecodePayload(string encodedData)
    {
        try
        {
            var padded = encodedData.Trim().Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var document = JsonDocument.Parse(json);
            return new GatewayPayload(document.RootElement.Clone(), json);
        }
        catch (Exception)
        {
            throw AppException.BadRequest("The payment response could not be read.", "invalid_callback");
        }
    }
}