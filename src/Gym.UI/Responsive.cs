using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Gym.UI;

/// <summary>
/// How big the UI is drawn. WPF already follows the Windows display scaling (per-monitor DPI, see app.manifest);
/// on top of that a big window (e.g. 1920 x 1080 at 100 %) draws everything larger, so text and buttons fill the screen
/// instead of floating at design size. The UI is never drawn smaller than design size: small screens get the compact
/// layout (<see cref="Responsive"/>) and scrolling content instead.
/// </summary>
public static class UiScale
{
    /// <summary>The window size (logical px) the layout is drawn at 100 % for (Admin, Cafeteria).</summary>
    public static readonly Size DefaultReference = new(1366, 820);

    /// <summary>Reception (touch, read from a distance): grows a bit earlier, so it is larger on big screens.</summary>
    public static readonly Size TouchReference = new(1280, 768);

    /// <summary>The largest scale (a 4K screen at 100 % stays usable instead of huge).</summary>
    public const double Max = 1.6;

    /// <summary>The scale for a window client area: min(width, height ratio to the reference), at least 1, at most <see cref="Max"/>.</summary>
    public static double For(Size client, Size reference)
    {
        if (!(client.Width > 0) || !(client.Height > 0) || double.IsInfinity(client.Width) || double.IsInfinity(client.Height)
            || !(reference.Width > 0) || !(reference.Height > 0))
        {
            return 1;
        }

        var scale = Math.Min(client.Width / reference.Width, client.Height / reference.Height);
        return Math.Round(Math.Clamp(scale, 1, Max), 2);
    }
}

/// <summary>
/// Layout breakpoints, set on the window content by <see cref="UiScaleHost"/> and inherited by every element:
/// <c>ui:Responsive.IsShort</c> (less than <see cref="ShortHeight"/> logical px high, e.g. 1366 x 768 at 100 %),
/// <c>ui:Responsive.IsVeryShort</c> (less than <see cref="VeryShortHeight"/>, e.g. 1366 x 768 at 125 %) and
/// <c>ui:Responsive.IsNarrow</c> (less than <see cref="NarrowWidth"/> wide, e.g. 1366 x 768 at 125 %). Styles use them in triggers
/// (<c>&lt;Trigger Property="ui:Responsive.IsShort" Value="True"&gt;</c>); <c>ui:Responsive.CollapseWhenShort="True"</c>
/// hides a decorative element (a big logo, a hint) on short windows.
/// </summary>
public static class Responsive
{
    public const double ShortHeight = 720;
    public const double VeryShortHeight = 600;
    public const double NarrowWidth = 1200;

    public static readonly DependencyProperty IsShortProperty = DependencyProperty.RegisterAttached(
        "IsShort", typeof(bool), typeof(Responsive),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, OnChanged));

    public static readonly DependencyProperty IsVeryShortProperty = DependencyProperty.RegisterAttached(
        "IsVeryShort", typeof(bool), typeof(Responsive),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, OnChanged));

    public static readonly DependencyProperty IsNarrowProperty = DependencyProperty.RegisterAttached(
        "IsNarrow", typeof(bool), typeof(Responsive),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, OnChanged));

    public static readonly DependencyProperty CollapseWhenShortProperty = DependencyProperty.RegisterAttached(
        "CollapseWhenShort", typeof(bool), typeof(Responsive), new PropertyMetadata(false, OnChanged));

    public static readonly DependencyProperty CollapseWhenVeryShortProperty = DependencyProperty.RegisterAttached(
        "CollapseWhenVeryShort", typeof(bool), typeof(Responsive), new PropertyMetadata(false, OnChanged));

    public static readonly DependencyProperty CollapseWhenNarrowProperty = DependencyProperty.RegisterAttached(
        "CollapseWhenNarrow", typeof(bool), typeof(Responsive), new PropertyMetadata(false, OnChanged));

    /// <summary>
    /// Marks a primary-styled button that is a step inside a scrolling form (Take photo next to the camera picture),
    /// not the page's main action: the layout check lets it scroll with the form.
    /// </summary>
    public static readonly DependencyProperty ScrollsWithContentProperty = DependencyProperty.RegisterAttached(
        "ScrollsWithContent", typeof(bool), typeof(Responsive), new PropertyMetadata(false));

    public static bool GetScrollsWithContent(DependencyObject element) => (bool)element.GetValue(ScrollsWithContentProperty);

    public static void SetScrollsWithContent(DependencyObject element, bool value) => element.SetValue(ScrollsWithContentProperty, value);

    public static bool GetIsShort(DependencyObject element) => (bool)element.GetValue(IsShortProperty);

    public static void SetIsShort(DependencyObject element, bool value) => element.SetValue(IsShortProperty, value);

    public static bool GetIsVeryShort(DependencyObject element) => (bool)element.GetValue(IsVeryShortProperty);

    public static void SetIsVeryShort(DependencyObject element, bool value) => element.SetValue(IsVeryShortProperty, value);

    public static bool GetCollapseWhenVeryShort(DependencyObject element) => (bool)element.GetValue(CollapseWhenVeryShortProperty);

    public static void SetCollapseWhenVeryShort(DependencyObject element, bool value) => element.SetValue(CollapseWhenVeryShortProperty, value);

    public static bool GetIsNarrow(DependencyObject element) => (bool)element.GetValue(IsNarrowProperty);

    public static void SetIsNarrow(DependencyObject element, bool value) => element.SetValue(IsNarrowProperty, value);

    public static bool GetCollapseWhenShort(DependencyObject element) => (bool)element.GetValue(CollapseWhenShortProperty);

    public static void SetCollapseWhenShort(DependencyObject element, bool value) => element.SetValue(CollapseWhenShortProperty, value);

    public static bool GetCollapseWhenNarrow(DependencyObject element) => (bool)element.GetValue(CollapseWhenNarrowProperty);

    public static void SetCollapseWhenNarrow(DependencyObject element, bool value) => element.SetValue(CollapseWhenNarrowProperty, value);

    /// <summary>Sets both breakpoints for a logical (unscaled) content size; infinite sizes leave them alone.</summary>
    internal static void Apply(DependencyObject element, Size logical)
    {
        if (!double.IsInfinity(logical.Height) && logical.Height > 0)
        {
            var isShort = logical.Height < ShortHeight;
            if (GetIsShort(element) != isShort)
            {
                SetIsShort(element, isShort);
            }

            var isVeryShort = logical.Height < VeryShortHeight;
            if (GetIsVeryShort(element) != isVeryShort)
            {
                SetIsVeryShort(element, isVeryShort);
            }
        }

        if (!double.IsInfinity(logical.Width) && logical.Width > 0)
        {
            var isNarrow = logical.Width < NarrowWidth;
            if (GetIsNarrow(element) != isNarrow)
            {
                SetIsNarrow(element, isNarrow);
            }
        }
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        var collapseShort = GetCollapseWhenShort(element);
        var collapseVeryShort = GetCollapseWhenVeryShort(element);
        var collapseNarrow = GetCollapseWhenNarrow(element);
        if (!collapseShort && !collapseVeryShort && !collapseNarrow)
        {
            return;
        }

        var hide = (collapseShort && GetIsShort(element))
                   || (collapseVeryShort && GetIsVeryShort(element))
                   || (collapseNarrow && GetIsNarrow(element));
        element.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
    }
}

/// <summary>
/// The window content host: draws the content <see cref="UiScale.For"/> times larger on big windows (a render
/// transform on a left-to-right wrapper, so the Arabic mirror stays inside it) and sets the
/// <see cref="Responsive"/> breakpoints from the logical size the content gets.
/// </summary>
public sealed class UiScaleHost : Decorator
{
    private readonly Border scaled;

    public UiScaleHost(UIElement content)
    {
        ArgumentNullException.ThrowIfNull(content);
        FlowDirection = FlowDirection.LeftToRight;
        scaled = new Border { FlowDirection = FlowDirection.LeftToRight, Child = content };
        Child = scaled;
    }

    /// <summary>The window size drawn at 100 % (<see cref="UiScale.DefaultReference"/> or <see cref="UiScale.TouchReference"/>).</summary>
    public Size Reference { get; set; } = UiScale.DefaultReference;

    /// <summary>The scale of the last layout (1 = design size).</summary>
    public double Scale { get; private set; } = 1;

    /// <summary>The size the content got in its own (unscaled) pixels at the last layout.</summary>
    public Size LogicalSize { get; private set; }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"scale {Scale:0.##}, content {LogicalSize.Width:0}x{LogicalSize.Height:0}");

    protected override Size MeasureOverride(Size constraint)
    {
        var scale = UiScale.For(constraint, Reference);
        var inner = new Size(constraint.Width / scale, constraint.Height / scale);
        Update(scale, inner);
        scaled.Measure(inner);
        return new Size(Math.Min(constraint.Width, scaled.DesiredSize.Width * scale), Math.Min(constraint.Height, scaled.DesiredSize.Height * scale));
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var scale = UiScale.For(arrangeSize, Reference);
        var inner = new Size(arrangeSize.Width / scale, arrangeSize.Height / scale);
        Update(scale, inner);
        scaled.Arrange(new Rect(inner));
        return arrangeSize;
    }

    private void Update(double scale, Size inner)
    {
        if (Math.Abs(scale - Scale) > 0.001)
        {
            Scale = scale;
            scaled.RenderTransform = scale == 1 ? Transform.Identity : new ScaleTransform(scale, scale);
        }

        if (!double.IsInfinity(inner.Width) && !double.IsInfinity(inner.Height))
        {
            LogicalSize = inner;
        }

        Responsive.Apply(scaled, inner);
    }
}
