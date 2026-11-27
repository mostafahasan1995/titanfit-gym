namespace Gym.Admin.Logic;

/// <summary>Phone numbers for display: Syrian mobiles as "0944 123 456" (show them left-to-right).</summary>
public static class PhoneText
{
    public static string Format(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return "—";
        }

        var p = phone.Trim();
        return p.Length == 10 && p.StartsWith("09", StringComparison.Ordinal) && p.All(char.IsAsciiDigit)
            ? $"{p[..4]} {p[4..7]} {p[7..]}"
            : p;
    }
}
