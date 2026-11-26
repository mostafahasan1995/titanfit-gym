namespace Gym.Domain;

/// <summary>Phone number cleanup and search keys.</summary>
public static class PhoneNumber
{
    /// <summary>Keeps a leading '+' and the digits; drops spaces, dashes, dots and brackets. Empty → null.</summary>
    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var trimmed = SearchText.NormalizeDigits(phone.Trim());
        var digits = SearchText.DigitsOnly(trimmed);
        if (digits.Length == 0)
        {
            return null;
        }

        return trimmed.StartsWith('+') ? "+" + digits : digits;
    }

    /// <summary>
    /// The part of a typed phone number to search for: digits only, without a leading 963 country code
    /// or 0 trunk prefix, so "0944 123 456", "+963 944 123 456" and "944123456" find the same member.
    /// </summary>
    public static string SearchKey(string? text)
    {
        var digits = SearchText.DigitsOnly(text);
        if (digits.StartsWith("00963", StringComparison.Ordinal))
        {
            digits = digits[5..];
        }
        else if (digits.StartsWith("963", StringComparison.Ordinal) && digits.Length > 9)
        {
            digits = digits[3..];
        }

        return digits.TrimStart('0');
    }
}
