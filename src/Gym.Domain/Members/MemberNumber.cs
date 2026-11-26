using System.Globalization;

namespace Gym.Domain;

/// <summary>Display format of the member number: <c>#00412</c>.</summary>
public static class MemberNumber
{
    public static string Format(int memberNo) => "#" + memberNo.ToString("D5", CultureInfo.InvariantCulture);

    /// <summary>Accepts "#00412", "00412", "412" and Arabic-Indic digits ("٤١٢").</summary>
    public static bool TryParse(string? text, out int memberNo)
    {
        memberNo = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var digits = SearchText.NormalizeDigits(text.Trim());
        if (digits.StartsWith('#'))
        {
            digits = digits[1..];
        }

        return digits.Length is > 0 and <= 9
            && digits.All(char.IsAsciiDigit)
            && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out memberNo)
            && memberNo > 0;
    }
}
