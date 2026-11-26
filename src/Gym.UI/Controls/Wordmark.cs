using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Gym.UI.Controls;

/// <summary>The TITANFIT wordmark: "TITAN" in white and "FIT" in brand cyan. Always left-to-right.</summary>
public sealed class Wordmark : TextBlock
{
    public Wordmark()
    {
        FlowDirection = FlowDirection.LeftToRight;
        FontWeight = FontWeights.Bold;
        SetResourceReference(FontFamilyProperty, "Font.Heading");
        SetResourceReference(ForegroundProperty, "Brush.Text");

        var fit = new Run("FIT");
        fit.SetResourceReference(TextElement.ForegroundProperty, "Brush.Brand");
        Inlines.Add(new Run("TITAN"));
        Inlines.Add(fit);
    }
}
