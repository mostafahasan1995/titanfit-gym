using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Gym.UI.Controls;

/// <summary>
/// A line icon from <see cref="UiIcons"/> (24 x 24 design grid), drawn in the inherited text colour:
/// <c>&lt;controls:Icon Data="{x:Static ui:UiIcons.Search}" Size="20" /&gt;</c>. In Arabic only the
/// directional icons (<see cref="UiIcons.IsDirectional"/>: arrows, chevrons) are mirrored.
/// </summary>
public sealed class Icon : FrameworkElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(Geometry), typeof(Icon), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnDataChanged));

    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(Icon),
        new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(Icon), new FrameworkPropertyMetadata(1.8, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(Icon), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public Geometry? Data
    {
        get => (Geometry?)GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>Width and height in pixels (default 20).</summary>
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Stroke on the 24 x 24 grid (default 1.8, like the design files).</summary>
    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public Brush? Foreground
    {
        get => (Brush?)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public Icon()
    {
        // Icons keep their drawing in Arabic (a mirrored check mark reads like a chevron); arrows and chevrons flip.
        FlowDirection = FlowDirection.LeftToRight;
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize) => new(Size, Size);

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var icon = (Icon)d;
        if (e.NewValue is Geometry geometry && UiIcons.IsDirectional(geometry))
        {
            icon.ClearValue(FlowDirectionProperty);
        }
        else
        {
            icon.FlowDirection = FlowDirection.LeftToRight;
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        if (Data is not { } data || Foreground is not { } brush)
        {
            return;
        }

        var pen = new Pen(brush, StrokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };
        var scale = Size / 24.0;
        drawingContext.PushTransform(new ScaleTransform(scale, scale));
        drawingContext.DrawGeometry(null, pen, data);
        drawingContext.Pop();
    }
}
