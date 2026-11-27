using System.Text;
using System.Windows.Input;

namespace Gym.Reception.Logic;

/// <summary>
/// Tells a USB barcode scanner in keyboard (HID, "keyboard wedge") mode from a person typing. A scanner types its whole
/// code in one burst, a few milliseconds per key, and ends with Enter; people type much slower. Feed every key with
/// its timestamp (<c>KeyEventArgs.Timestamp</c>, milliseconds). <see cref="Enter"/> returns the scanned code when all
/// the keys since the last pause came fast enough, otherwise null (a person typed: let the normal Enter run).
/// </summary>
public sealed class KeyboardWedge
{
    /// <summary>Scanners send a key every 1 to 20 ms; a fast typist needs about 80 ms.</summary>
    public const int DefaultMaxGapMs = 50;

    public const int DefaultMinLength = 3;

    private readonly StringBuilder buffer = new();
    private long lastKeyAt;

    public KeyboardWedge(int maxGapMs = DefaultMaxGapMs, int minLength = DefaultMinLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxGapMs, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(minLength, 1);
        MaxGapMs = maxGapMs;
        MinLength = minLength;
    }

    public int MaxGapMs { get; }

    public int MinLength { get; }

    /// <summary>True while a burst is being collected (the last key came less than <see cref="MaxGapMs"/> ago).</summary>
    public bool IsCollecting(long timestampMs) => buffer.Length > 0 && timestampMs - lastKeyAt <= MaxGapMs;

    /// <summary>One typed character. A pause longer than <see cref="MaxGapMs"/> starts a new burst.</summary>
    public void Press(char character, long timestampMs)
    {
        if (buffer.Length > 0 && timestampMs - lastKeyAt > MaxGapMs)
        {
            buffer.Clear();
        }

        buffer.Append(character);
        lastKeyAt = timestampMs;
    }

    /// <summary>Enter was pressed: the scanned code, or null when the keys were typed by a person (or too few).</summary>
    public string? Enter(long timestampMs)
    {
        var scanned = buffer.Length >= MinLength && timestampMs - lastKeyAt <= MaxGapMs ? buffer.ToString() : null;
        buffer.Clear();
        return scanned;
    }

    public void Reset() => buffer.Clear();

    /// <summary>
    /// The character of a key for a member code ("TF1-…" or a member number), by key position, so an Arabic keyboard
    /// layout does not turn the scanner's letters into Arabic ones. Null for keys that are not part of a code.
    /// </summary>
    public static char? CharFor(Key key, bool shift)
    {
        if (key >= Key.D0 && key <= Key.D9)
        {
            return shift ? null : (char)('0' + (key - Key.D0));
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return (char)('0' + (key - Key.NumPad0));
        }

        if (key >= Key.A && key <= Key.Z)
        {
            return (char)('A' + (key - Key.A));
        }

        return key == Key.Subtract || (key == Key.OemMinus && !shift) ? '-' : null;
    }
}
