using System.Security.Cryptography;

namespace Backend.Common;

public static class CodeGenerator
{
    // No 0/O, 1/I/L: easy to read out over the phone
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const string PasswordLetters = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ";
    private const string PasswordDigits = "23456789";

    /// <summary>e.g. "FB-8K3D2Q"</summary>
    public static string ReferenceCode(string? prefix, int length = 6)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        }

        var cleanPrefix = string.IsNullOrWhiteSpace(prefix) ? string.Empty : $"{prefix.Trim().ToUpperInvariant()}-";
        return cleanPrefix + new string(chars);
    }

    /// <summary>Temporary password with at least one letter and one digit (meets the change-password rules).</summary>
    public static string TemporaryPassword(int length = 10)
    {
        if (length < 8) length = 8;
        var all = PasswordLetters + PasswordDigits;
        var chars = new char[length];

        chars[0] = PasswordLetters[RandomNumberGenerator.GetInt32(PasswordLetters.Length)];
        chars[1] = PasswordDigits[RandomNumberGenerator.GetInt32(PasswordDigits.Length)];
        for (var i = 2; i < length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        // Fisher–Yates shuffle so the letter/digit aren't always first
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}