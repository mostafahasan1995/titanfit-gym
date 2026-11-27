using Gym.Domain;

namespace Gym.Reception.Logic;

/// <summary>
/// The phone field (+963 in front). Staff type "944 123 456", "0944 123 456", "+963 944 123 456" or "00963…";
/// a Syrian mobile is stored as "0944123456" like the members already in the system. Anything else is cleaned like
/// the server does (<see cref="PhoneNumber.Normalize"/>: digits and a leading +). The search finds both forms.
/// </summary>
public static class ReceptionPhone
{
    public const string CountryCode = "+963";

    /// <summary>The stored form of what was typed, or null when it holds no digits.</summary>
    public static string? Normalize(string? input)
    {
        var cleaned = PhoneNumber.Normalize(input);
        if (cleaned is null)
        {
            return null;
        }

        var national = PhoneNumber.SearchKey(cleaned);
        return IsSyrianMobile(national) ? "0" + national : cleaned;
    }

    /// <summary>True for a Syrian mobile number: 9 digits starting with 9 after +963 or the leading 0.</summary>
    public static bool LooksLikeMobile(string? input) => IsSyrianMobile(PhoneNumber.SearchKey(input));

    /// <summary>"0944123456" → "0944 123 456"; other numbers are shown as stored.</summary>
    public static string Format(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return "";
        }

        var national = PhoneNumber.SearchKey(phone);
        return IsSyrianMobile(national) ? $"0{national[..3]} {national[3..6]} {national[6..]}" : phone.Trim();
    }

    /// <summary>The part after +963 for the input box: "944 123 456" (empty for no phone).</summary>
    public static string ForInput(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return "";
        }

        var national = PhoneNumber.SearchKey(phone);
        return IsSyrianMobile(national) ? $"{national[..3]} {national[3..6]} {national[6..]}" : phone.Trim();
    }

    private static bool IsSyrianMobile(string national) => national.Length == 9 && national[0] == '9';
}
