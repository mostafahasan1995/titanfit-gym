using System.Windows;
using System.Windows.Controls;

namespace Gym.UI.Controls;

/// <summary>
/// Lays its children out side by side in up to <see cref="Columns"/> columns (with <see cref="Gap"/> between them; equal,
/// or shared by <c>controls:ResponsiveColumns.Weight</c> of the children in a row),
/// and in fewer columns when a column would be narrower than <see cref="MinColumnWidth"/>: on a narrow window two form
/// columns become one, a row of four cards becomes two rows of two. Rows are as high as their highest child.
/// </summary>
public sealed class ResponsiveColumns : Panel
{
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(
        nameof(Columns), typeof(int), typeof(ResponsiveColumns),
        new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure), v => (int)v >= 1);

    public static readonly DependencyProperty MinColumnWidthProperty = DependencyProperty.Register(
        nameof(MinColumnWidth), typeof(double), typeof(ResponsiveColumns),
        new FrameworkPropertyMetadata(280.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(ResponsiveColumns),
        new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty RowGapProperty = DependencyProperty.Register(
        nameof(RowGap), typeof(double), typeof(ResponsiveColumns),
        new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty WeightProperty = DependencyProperty.RegisterAttached(
        "Weight", typeof(double), typeof(ResponsiveColumns),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsParentMeasure), v => (double)v > 0);

    public static double GetWeight(DependencyObject element) => (double)element.GetValue(WeightProperty);

    public static void SetWeight(DependencyObject element, double value) => element.SetValue(WeightProperty, value);

    /// <summary>The most columns (default 2).</summary>
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>Below this width per column, use one column less (default 280).</summary>
    public double MinColumnWidth
    {
        get => (double)GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    /// <summary>Space between columns (default 16).</summary>
    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <summary>Space between rows (default 16).</summary>
    public double RowGap
    {
        get => (double)GetValue(RowGapProperty);
        set => SetValue(RowGapProperty, value);
    }

    /// <summary>How many columns fit a width (at least 1).</summary>
    public static int ColumnsFor(double width, int columns, double minColumnWidth, double gap)
    {
        if (double.IsInfinity(width) || double.IsNaN(width))
        {
            return Math.Max(1, columns);
        }

        var count = Math.Max(1, columns);
        while (count > 1 && (width - (gap * (count - 1))) / count < minColumnWidth)
        {
            count--;
        }

        return count;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = VisibleChildren();
        if (children.Count == 0)
        {
            return default;
        }

        var columns = ColumnCount(children, availableSize.Width);
        double height = 0;
        double widest = 0;
        for (var start = 0; start < children.Count; start += columns)
        {
            var row = children.Skip(start).Take(columns).ToList();
            var widths = Widths(row, availableSize.Width);
            double rowHeight = 0;
            for (var i = 0; i < row.Count; i++)
            {
                row[i].Measure(new Size(widths[i], double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, row[i].DesiredSize.Height);
                widest = Math.Max(widest, row[i].DesiredSize.Width);
            }

            height += rowHeight + (start > 0 ? RowGap : 0);
        }

        var desiredWidth = double.IsInfinity(availableSize.Width) ? (widest * columns) + (Gap * (columns - 1)) : availableSize.Width;
        return new Size(desiredWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = VisibleChildren();
        if (children.Count == 0)
        {
            return finalSize;
        }

        var columns = ColumnCount(children, finalSize.Width);
        double y = 0;
        for (var start = 0; start < children.Count; start += columns)
        {
            var row = children.Skip(start).Take(columns).ToList();
            var widths = Widths(row, finalSize.Width);
            for (var i = 0; i < row.Count; i++)
            {
                row[i].Measure(new Size(widths[i], double.PositiveInfinity));
            }

            var rowHeight = row.Max(c => c.DesiredSize.Height);
            double x = 0;
            for (var i = 0; i < row.Count; i++)
            {
                row[i].Arrange(new Rect(x, y, widths[i], rowHeight));
                x += widths[i] + Gap;
            }

            y += rowHeight + RowGap;
        }

        return finalSize;
    }

    /// <summary>The most columns (up to <see cref="Columns"/>) whose narrowest column is still at least <see cref="MinColumnWidth"/>.</summary>
    private int ColumnCount(List<UIElement> children, double width)
    {
        var count = Math.Min(children.Count, Math.Max(1, Columns));
        if (double.IsInfinity(width) || double.IsNaN(width))
        {
            return count;
        }

        while (count > 1)
        {
            var fits = true;
            for (var start = 0; start < children.Count && fits; start += count)
            {
                fits = Widths(children.Skip(start).Take(count).ToList(), width).Min() >= MinColumnWidth;
            }

            if (fits)
            {
                break;
            }

            count--;
        }

        return count;
    }

    /// <summary>The widths of one row: the space after the gaps, shared by the children's weights.</summary>
    private double[] Widths(List<UIElement> row, double total)
    {
        if (double.IsInfinity(total) || double.IsNaN(total))
        {
            return row.Select(_ => double.PositiveInfinity).ToArray();
        }

        var space = Math.Max(0, total - (Gap * (row.Count - 1)));
        var weights = row.Select(GetWeight).ToArray();
        var sum = weights.Sum();
        return weights.Select(w => space * w / sum).ToArray();
    }

    private List<UIElement> VisibleChildren() =>
        InternalChildren.Cast<UIElement>().Where(c => c is not null && c.Visibility != Visibility.Collapsed).ToList();
}
