using Backend.Models;

namespace Backend.Common;

public static class DisplayText
{
    /// <summary>The DB stores Cash/Online; the frontend shows Cash/Esewa.</summary>
    public static string PaymentMethodLabel(PaymentMethod method) =>
        method == PaymentMethod.Online ? "Esewa" : "Cash";

    /// <summary>Accepts "Cash", "Online" or "Esewa" from a request.</summary>
    public static PaymentMethod ParsePaymentMethod(string? value)
    {
        var text = (value ?? string.Empty).Trim().ToLowerInvariant();
        return text switch
        {
            "cash" => PaymentMethod.Cash,
            "online" or "esewa" or "e-sewa" => PaymentMethod.Online,
            _ => throw AppException.Validation("paymentMethod", "Choose either Cash or eSewa."),
        };
    }

    /// <summary>Parses an optional enum filter; returns null when the text is blank or unknown.</summary>
    public static TEnum? ParseEnumFilter<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed) ? parsed : null;
}