using System.Globalization;

namespace Gym.Tests.Domain;

internal static class Dates
{
    /// <summary>Parses "yyyy-MM-dd".</summary>
    public static DateOnly D(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
