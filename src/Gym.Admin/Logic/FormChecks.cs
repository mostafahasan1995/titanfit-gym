using Gym.Domain;
using Gym.UI;

namespace Gym.Admin.Logic;

/// <summary>Reads what the admin typed. Each check gives the value or the message to show under the input.</summary>
public static class FormChecks
{
    /// <summary>A whole number from <paramref name="min"/> to <paramref name="max"/> (Western or Arabic-Indic digits).</summary>
    public static (int? Value, LocalText? Error) WholeNumber(string? text, int min, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, AdminText.Required);
        }

        var digits = SearchText.NormalizeDigits(text.Trim()).Replace(",", "", StringComparison.Ordinal).Replace("٬", "", StringComparison.Ordinal);
        if (!int.TryParse(digits, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return (null, AdminText.WholeNumber);
        }

        return value < min || value > max ? (null, RangeError(min, max)) : (value, null);
    }

    /// <summary>An amount in whole SYP, 0 or more ("150000", "150,000", "١٥٠٬٠٠٠").</summary>
    public static (long? Value, LocalText? Error) Amount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, AdminText.Required);
        }

        return Money.Parse(text) is { } amount and >= 0 ? (amount, null) : (null, AdminText.AmountInvalid);
    }

    /// <summary>Trimmed text, or the "required" message when empty.</summary>
    public static (string? Value, LocalText? Error) Required(string? text) =>
        string.IsNullOrWhiteSpace(text) ? (null, AdminText.Required) : (text.Trim(), null);

    /// <summary>"Enter a number from 0 to 60." with the numbers filled in (both languages).</summary>
    public static LocalText RangeError(int min, int max) => new(
        AdminText.NumberRange.Format(AppLanguage.English, min, max),
        AdminText.NumberRange.Format(AppLanguage.Arabic, min, max));

    /// <summary>Empty text means null; otherwise trimmed.</summary>
    public static string? Optional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>The settings page form: expiring-soon days (0 to 60) and the private training fee (whole SYP).</summary>
public static class SettingsForm
{
    public const int MaxExpiringSoonDays = GymSettings.MaxExpiringSoonDays;

    public sealed record Result(int? ExpiringSoonDays, long? PrivateTrainingFee, LocalText? DaysError, LocalText? FeeError)
    {
        public bool IsValid => DaysError is null && FeeError is null;
    }

    public static Result Validate(string? days, string? fee)
    {
        var (dayValue, dayError) = FormChecks.WholeNumber(days, 0, MaxExpiringSoonDays);
        var (feeValue, feeError) = FormChecks.Amount(fee);
        return new Result(dayValue, feeValue, dayError, feeError);
    }
}
