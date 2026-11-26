using System.Reflection;
using System.Text.RegularExpressions;

namespace Gym.UI;

/// <summary>
/// Checks every <see cref="LocalText"/> held in a static property or field of a static class (the strings classes):
/// both languages filled in, the Arabic really Arabic, and the same <c>{0}</c> placeholders in both.
/// The unit tests run it on Gym.UI; the smoke screenshots run it on each app, so a missing translation fails CI.
/// </summary>
public static partial class LocalTextAudit
{
    /// <summary>All strings of the static classes in <paramref name="assembly"/>, named "Class.Property".</summary>
    public static IReadOnlyList<(string Name, LocalText Text)> FindTexts(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).Select(t => t!).ToArray();
        }

        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var found = new List<(string, LocalText)>();
        foreach (var type in types.Where(t => t is { IsAbstract: true, IsSealed: true, IsGenericTypeDefinition: false }))
        {
            foreach (var property in type.GetProperties(Static).Where(p => p.PropertyType == typeof(LocalText) && p.GetIndexParameters().Length == 0))
            {
                if (property.GetValue(null) is LocalText text)
                {
                    found.Add(($"{type.Name}.{property.Name}", text));
                }
            }

            foreach (var field in type.GetFields(Static).Where(f => f.FieldType == typeof(LocalText) && !f.Name.Contains('<', StringComparison.Ordinal)))
            {
                if (field.GetValue(null) is LocalText text)
                {
                    found.Add(($"{type.Name}.{field.Name}", text));
                }
            }
        }

        return found;
    }

    /// <summary>One line per problem, empty when all strings are fine.</summary>
    public static IReadOnlyList<string> FindProblems(Assembly assembly) =>
        FindTexts(assembly).SelectMany(t => Check(t.Name, t.Text)).ToList();

    public static IEnumerable<string> Check(string name, LocalText text)
    {
        if (string.IsNullOrWhiteSpace(text.En))
        {
            yield return $"{name}: the English text is empty.";
        }

        if (string.IsNullOrWhiteSpace(text.Ar))
        {
            yield return $"{name}: the Arabic text is empty.";
        }
        else if (text.En.Any(char.IsLetter) && !text.Ar.Any(IsArabicLetter))
        {
            yield return $"{name}: the Arabic text has no Arabic letters (\"{text.Ar}\").";
        }

        var en = Placeholders(text.En ?? "");
        var ar = Placeholders(text.Ar ?? "");
        if (!en.SequenceEqual(ar))
        {
            yield return $"{name}: the placeholders differ (en: {string.Join(" ", en)}, ar: {string.Join(" ", ar)}).";
        }
    }

    private static bool IsArabicLetter(char c) => c is >= '؀' and <= 'ۿ' && char.IsLetter(c);

    private static List<string> Placeholders(string text) =>
        PlaceholderPattern().Matches(text).Select(m => m.Value).Distinct().Order(StringComparer.Ordinal).ToList();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderPattern();
}
