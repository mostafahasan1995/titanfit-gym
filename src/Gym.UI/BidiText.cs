using System.Windows;
using System.Windows.Controls;

namespace Gym.UI;

/// <summary>
/// Data text (names, station names, codes) in the Arabic layout: text without Arabic letters is shown left-to-right,
/// so "Reception 1" does not turn into "1 Reception". Give the TextBlock <c>HorizontalAlignment="Left"</c> (the start side).
/// </summary>
public static class BidiText
{
    /// <summary>True when the text has Arabic (right-to-left) letters.</summary>
    public static bool HasRtl(string? text) => text is not null && text.Any(c => c is >= '\u0590' and <= '\u08FF' or >= '\uFB1D' and <= '\uFEFC');

    /// <summary>Sets the text and its direction: its own when it has Arabic letters, otherwise left-to-right.</summary>
    public static void SetText(TextBlock block, string? text)
    {
        ArgumentNullException.ThrowIfNull(block);
        block.Text = text ?? "";
        if (HasRtl(text))
        {
            block.ClearValue(FrameworkElement.FlowDirectionProperty);
        }
        else
        {
            block.FlowDirection = FlowDirection.LeftToRight;
        }
    }
}
