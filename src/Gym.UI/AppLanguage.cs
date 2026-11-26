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
        "en" => AppLanguage.English,
        "ar" => AppLanguage.Arabic,
        _ => throw new ArgumentException($"Unknown language '{value}'. Use en or ar.", nameof(value)),
    };

    /// <summary>Reads <c>--lang en|ar</c> from the command line. Missing means English.</summary>
    public static AppLanguage FromArgs(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], "--lang", StringComparison.OrdinalIgnoreCase))
            {
                return i + 1 < args.Count
                    ? Parse(args[i + 1])
                    : throw new ArgumentException("--lang needs a value: en or ar.");
            }
        }

        return AppLanguage.English;
    }

    public static FlowDirection ToFlowDirection(this AppLanguage language) =>
        language == AppLanguage.Arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public static XmlLanguage ToXmlLanguage(this AppLanguage language) =>
        XmlLanguage.GetLanguage(language == AppLanguage.Arabic ? "ar-SY" : "en-US");
}

/// <summary>A UI string in English and Arabic.</summary>
public readonly record struct LocalText(string En, string Ar)
{
    public string For(AppLanguage language) => language == AppLanguage.Arabic ? Ar : En;
}
