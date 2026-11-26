using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace Gym.UI;

/// <summary>UI language. Arabic is right-to-left.</summary>
public enum AppLanguage
{
    English,
    Arabic,
}

public static class AppLanguages
{
    /// <summary>Parses "en" or "ar".</summary>
    public static AppLanguage Parse(string value) => value.Trim().ToLowerInvariant() switch
    {
        "en" or "english" => AppLanguage.English,
        "ar" or "arabic" => AppLanguage.Arabic,
        _ => throw new ArgumentException($"Unknown language '{value}'. Use en or ar.", nameof(value)),
    };

    /// <summary>"en" or "ar".</summary>
    public static string ToCode(this AppLanguage language) => language == AppLanguage.Arabic ? "ar" : "en";

    /// <summary>Reads <c>--lang en|ar</c> from the command line. Missing means null.</summary>
    public static AppLanguage? FromArgs(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], "--lang", StringComparison.OrdinalIgnoreCase))
            {
                return i + 1 < args.Count
                    ? Parse(args[i + 1])
                    : throw new ArgumentException("--lang needs a value: en or ar.");
            }
        }

        return null;
    }

    public static FlowDirection ToFlowDirection(this AppLanguage language) =>
        language == AppLanguage.Arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public static XmlLanguage ToXmlLanguage(this AppLanguage language) =>
        XmlLanguage.GetLanguage(language == AppLanguage.Arabic ? "ar-SY" : "en-US");
}

/// <summary>
/// A UI string in English and Arabic. Every visible string of the apps is one of these, kept in a static strings
/// class (<see cref="CommonText"/> for the shared ones, e.g. <c>ReceptionText</c> in an app). In XAML use
/// <c>{ui:Loc {x:Static local:ReceptionText.Title}}</c>; in code <c>ReceptionText.Title.For(UiLanguage.Current)</c>.
/// </summary>
public readonly record struct LocalText(string En, string Ar)
{
    public string For(AppLanguage language) => language == AppLanguage.Arabic ? Ar : En;

    /// <summary>The text in the current UI language.</summary>
    public string Current => For(UiLanguage.Current);

    /// <summary><see cref="string.Format(IFormatProvider, string, object[])"/> with Western digits, e.g. <c>"{0} members"</c>.</summary>
    public string Format(AppLanguage language, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, For(language), args);

    public override string ToString() => Current;
}
