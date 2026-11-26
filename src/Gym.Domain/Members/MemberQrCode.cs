using System.Buffers;
using System.Security.Cryptography;

namespace Gym.Domain;

/// <summary>
/// The member's QR code text: a prefix and 24 random Crockford base32 characters (120 bits), e.g.
/// <c>TF1-7KQ2M9...</c>. It holds no personal data. Upper case, digits and '-' keep the QR in the compact
/// alphanumeric mode, so a phone screen scans fast.
/// </summary>
public static class MemberQrCode
{
    public const string Prefix = "TF1-";
    public const int RandomLength = 24;
    public const int TotalLength = 28;

    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private static readonly SearchValues<char> AlphabetValues = SearchValues.Create(Alphabet);

    public static string Generate() => Prefix + RandomNumberGenerator.GetString(Alphabet, RandomLength);

    /// <summary>Trims and upper-cases a scanned code.</summary>
    public static string Normalize(string? code) => (code ?? "").Trim().ToUpperInvariant();

    public static bool IsWellFormed(string? code) =>
        code is { Length: TotalLength }
        && code.StartsWith(Prefix, StringComparison.Ordinal)
        && !code.AsSpan(Prefix.Length).ContainsAnyExcept(AlphabetValues);
}
