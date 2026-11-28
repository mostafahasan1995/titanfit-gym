using System.Windows;
using System.Windows.Controls;

namespace Gym.UI.Controls;

/// <summary>
/// A toolbar row: the first child at the start, the other children together at the end (like a DockPanel with the
/// rest docked to the right). When they do not fit side by side, each child goes on its own line, at the start,
/// with <see cref="RowGap"/> between the lines: on a narrow window the buttons wrap below the filters instead of
/// being cut off. Children keep their own size (a child wider than the row is given the row's width).
/// </summary>
public sealed class SplitRow : Panel
{
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(SplitRow), new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty RowGapProperty = DependencyProperty.Register(
        nameof(RowGap), typeof(double), typeof(SplitRow), new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>Space between the start child and the end group, and between the end children (default 12).</summary>
    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <summary>Space between the lines when the children wrap (default 10).</summary>
    public double RowGap
    {
        get => (double)GetValue(RowGapProperty);
        set => SetValue(RowGapProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = Visible();
        foreach (var child in children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
        }

        if (children.Count == 0)
        {
            return default;
        }

        if (Fits(children, availableSize.Width))
        {
            return new Size(
                double.IsInfinity(availableSize.Width) ? OneLineWidth(children) : availableSize.Width,
                children.Max(c => c.DesiredSize.Height));
        }

        return new Size(
            children.Max(c => c.DesiredSize.Width),
            children.Sum(c => c.DesiredSize.Height) + (RowGap * (children.Count - 1)));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Visible();
        if (children.Count == 0)
        {
            return finalSize;
        }

        if (Fits(children, finalSize.Width))
        {
            var height = finalSize.Height;
            children[0].Arrange(new Rect(0, 0, children[0].DesiredSize.Width, height));
            var x = finalSize.Width;
            for (var i = children.Count - 1; i >= 1; i--)
            {
                x -= children[i].DesiredSize.Width;
                children[i].Arrange(new Rect(x, 0, children[i].DesiredSize.Width, height));
                x -= Gap;
            }

            return finalSize;
        }

        double y = 0;
        foreach (var child in children)
        {
            child.Arrange(new Rect(0, y, Math.Min(child.DesiredSize.Width, finalSize.Width), child.DesiredSize.Height));
            y += child.DesiredSize.Height + RowGap;
        }

        return finalSize;
    }

    private bool Fits(List<UIElement> children, double width) =>
        double.IsInfinity(width) || OneLineWidth(children) <= width + 0.5;

    private double OneLineWidth(List<UIElement> children) =>
        children.Sum(c => c.DesiredSize.Width) + (Gap * (children.Count - 1));

    private List<UIElement> Visible() =>
        InternalChildren.Cast<UIElement>().Where(c => c is not null && c.Visibility != Visibility.Collapsed).ToList();
}
