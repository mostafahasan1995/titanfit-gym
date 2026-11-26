namespace Gym.Infrastructure.Services;

/// <summary>Input checks for request fields. Errors use the code <c>validation</c>.</summary>
internal static class Guard
{
    public static string Required(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException("validation", $"{field} is required.");
        }

        return MaxLength(trimmed, field, maxLength);
    }

    public static string? Optional(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : MaxLength(trimmed, field, maxLength);
    }

    public static T Defined<T>(T value, string field)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new DomainException("validation", $"{field} is not valid.");
        }

        return value;
    }

    public static int Range(int value, string field, int min, int max)
    {
        if (value < min || value > max)
        {
            throw new DomainException("validation", $"{field} must be {min} to {max}.");
        }

        return value;
    }

    public static long NotNegative(long value, string field)
    {
        if (value < 0)
        {
            throw new DomainException("validation", $"{field} cannot be negative.");
        }

        return value;
    }

    public static (int Page, int PageSize) Paging(int page, int pageSize, int maxPageSize = 100) =>
        (Math.Max(page, 1), Math.Clamp(pageSize, 1, maxPageSize));

    /// <summary>Escapes % _ and \ for a LIKE/ILIKE pattern.</summary>
    public static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private static string MaxLength(string value, string field, int maxLength) =>
        value.Length <= maxLength
            ? value
            : throw new DomainException("validation", $"{field} can be at most {maxLength} characters.");
}
