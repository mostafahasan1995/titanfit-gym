using System.Text;

namespace Gym.Cafeteria.Hardware;

/// <summary>
/// Recognises a USB barcode scanner in keyboard (HID) mode: it "types" the code very fast and ends with Enter.
/// Feed it every typed character with the key's timestamp (milliseconds, e.g. <c>TextCompositionEventArgs.Timestamp</c>)
/// and call <see cref="Complete"/> on Enter. People type far slower than <see cref="MaxGap"/> between keys, so a
/// burst of at least <see cref="MinLength"/> characters ending in Enter is a scan; anything else is normal typing.
/// Pure logic (no WPF), so it is unit tested.
/// </summary>
public sealed class BarcodeWedge
{
    public const int DefaultMaxGapMs = 60;
    public const int DefaultMinLength = 4;
    public const int MaxLength = 64;

    private readonly StringBuilder buffer = new();
    private long lastAt;

    public BarcodeWedge(int maxGapMs = DefaultMaxGapMs, int minLength = DefaultMinLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxGapMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minLength);
        MaxGap = maxGapMs;
        MinLength = minLength;
    }

    /// <summary>Longest pause between two scanner keys, in milliseconds.</summary>
    public int MaxGap { get; }

    /// <summary>Shortest code that counts as a scan (EAN-8 has 8 digits; short internal codes are allowed).</summary>
    public int MinLength { get; }

    /// <summary>The characters of the burst in progress.</summary>
    public string Pending => buffer.ToString();

    /// <summary>
    /// A typed character (or several, for one text input event) at <paramref name="timestampMs"/>.
    /// True when it starts a new burst: the caller can remember the focused text box's text then, to undo the
    /// scanner's typing in it when the burst turns out to be a scan.
    /// </summary>
    public bool Feed(string text, long timestampMs)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var startsBurst = buffer.Length == 0 || timestampMs - lastAt > MaxGap || timestampMs < lastAt;
        if (startsBurst)
        {
            buffer.Clear();
        }

        foreach (var c in text)
        {
            if (!char.IsControl(c) && buffer.Length < MaxLength)
            {
                buffer.Append(c);
            }
        }

        lastAt = timestampMs;
        return startsBurst;
    }

    /// <summary>
    /// Enter was pressed at <paramref name="timestampMs"/>. Returns the scanned code when the keys before it were a
    /// scanner burst, otherwise null (normal typing; let Enter do its usual job). Always starts over.
    /// </summary>
    public string? Complete(long timestampMs)
    {
        var code = buffer.ToString().Trim();
        var fast = buffer.Length > 0 && timestampMs - lastAt <= MaxGap && timestampMs >= lastAt;
        Reset();
        return fast && code.Length >= MinLength ? code : null;
    }

    public void Reset() => buffer.Clear();
}
