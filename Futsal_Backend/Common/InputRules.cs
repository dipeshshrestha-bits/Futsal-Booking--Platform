using System.Text.RegularExpressions;

namespace Backend.Common;

/// <summary>Validation rules shared with the frontend so both sides agree.</summary>
public static partial class InputRules
{
    [GeneratedRegex(@"^9[678]\d{8}$")]
    private static partial Regex NepalMobileRegex();

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"[\s-]")]
    private static partial Regex PhoneSeparatorsRegex();

    /// <summary>"+977 98-1234 5678" → "9812345678"</summary>
    public static string NormalizePhone(string? value)
    {
        var digits = PhoneSeparatorsRegex().Replace(value ?? string.Empty, string.Empty);
        if (digits.StartsWith("+977")) digits = digits[4..];
        else if (digits.StartsWith("977") && digits.Length == 13) digits = digits[3..];
        return digits;
    }

    public static bool IsValidNepalPhone(string? value) => NepalMobileRegex().IsMatch(NormalizePhone(value));

    public static bool IsValidEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && EmailRegex().IsMatch(value.Trim());

    /// <summary>At least 8 characters, one letter and one number.</summary>
    public static bool IsStrongPassword(string? value) =>
        value is { Length: >= 8 } && value.Any(char.IsLetter) && value.Any(char.IsDigit);

    /// <summary>Trims text and turns blank strings into null.</summary>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}