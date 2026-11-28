using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Gym.UI;

/// <summary>The result of <see cref="LayoutAudit.Check"/> for one screen.</summary>
public sealed record LayoutReport(int Checked, int Primary, IReadOnlyList<string> Problems)
{
    public bool Ok => Problems.Count == 0;
}

/// <summary>
/// The layout check of the smoke screenshots: every visible interactive control (buttons, toggles, text and password
/// boxes, combo boxes, date pickers) must lie inside the window and must not be cut off by a parent that is too small.
/// A control inside a scrolling area may be outside its viewport (the user scrolls to it), but only along an axis
/// that scrolls. Primary buttons (<c>Button.Primary</c>, <c>Button.Gold</c>, <c>Button.Danger</c>) that are not part of
/// a list must be fully in view without scrolling: Save, Pay, Check in and the like always stay visible.
/// Lists and tables (ListBox, DataGrid) must keep room for at least one row (<see cref="MinListHeight"/>) in view, and
/// a table's columns must fit its width.
/// </summary>
public static class LayoutAudit
{
    /// <summary>Rounding slack in logical px.</summary>
    private const double Slack = 1.5;

    /// <summary>A list or table squeezed below this (logical px) cannot show a row: reported.</summary>
    public const double MinListHeight = 44;

    private static readonly string[] PrimaryStyleKeys = ["Button.Primary", "Button.Gold", "Button.Danger"];

    public static LayoutReport Check(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var primaryStyles = PrimaryStyleKeys
            .Select(key => (Key: key, Style: root.TryFindResource(key) as Style))
            .Where(p => p.Style is not null)
            .ToList();
        var window = new Rect(root.RenderSize);
        var problems = new List<string>();
        var count = 0;
        var primary = 0;
        foreach (var control in Visible(root).OfType<Control>().Where(IsChecked))
        {
            count++;
            var styleKey = PrimaryKey(control.Style, primaryStyles);
            var strict = styleKey is not null && !IsInList(control, root) && !Responsive.GetScrollsWithContent(control);
            if (strict)
            {
                primary++;
            }

            if (Find(control, root, window, strict) is { } problem)
            {
                problems.Add($"{Describe(control, styleKey)} at {Format(Bounds(control, root))}: {problem}");
            }
        }

        foreach (var list in Visible(root).OfType<Selector>().Where(l => (l is ListBox or DataGrid) && (l.TemplatedParent is null or ContentPresenter)))
        {
            count++;
            if (ListProblem(list, root, window) is { } problem)
            {
                problems.Add($"{Describe(list, null)} at {Format(Bounds(list, root))}: {problem}");
            }
        }

        return new LayoutReport(count, primary, problems);
    }

    /// <summary>
    /// A table must not need sideways scrolling (its columns fit), and a list outside any scrolling area must show at
    /// least <see cref="MinListHeight"/> px of itself.
    /// </summary>
    private static string? ListProblem(Selector list, FrameworkElement root, Rect window)
    {
        if (list is DataGrid grid && grid.Template?.FindName("DG_ScrollViewer", grid) is ScrollViewer { ViewportWidth: > 0 } tableScroll)
        {
            var columns = grid.Columns.Where(c => c.Visibility == Visibility.Visible).Sum(c => c.ActualWidth);
            if (columns > tableScroll.ViewportWidth + Slack)
            {
                return string.Create(CultureInfo.InvariantCulture,
                    $"the columns need {columns:0} px but the table is {tableScroll.ViewportWidth:0} px wide (columns cut off)");
            }
        }

        var visible = Bounds(list, root);
        DependencyObject current = list;
        while (!ReferenceEquals(current, root) && VisualTreeHelper.GetParent(current) is { } parent)
        {
            current = parent;
            if (current is ScrollContentPresenter)
            {
                return null; // inside a scrolling area: the user scrolls to it.
            }

            if (current is UIElement element && ClipOf(element) is { } clip)
            {
                visible.Intersect(element.TransformToAncestor(root).TransformBounds(clip));
                if (visible.IsEmpty)
                {
                    break;
                }
            }
        }

        if (!visible.IsEmpty)
        {
            visible.Intersect(window);
        }

        var shown = visible.IsEmpty ? 0 : visible.Height;
        return shown + Slack < MinListHeight
            ? string.Create(CultureInfo.InvariantCulture, $"only {shown:0} px of the list is visible (no room for its rows)")
            : null;
    }

    /// <summary>The controls the user works with; template parts of other checked controls and of scroll bars are left out.</summary>
    private static bool IsChecked(Control control)
    {
        if (control is not (ButtonBase or TextBoxBase or PasswordBox or ComboBox or DatePicker) || control is DataGridColumnHeader)
        {
            return false;
        }

        return control.TemplatedParent is not (ButtonBase or TextBoxBase or ComboBox or DatePicker or ScrollBar or ScrollViewer or DataGrid or Slider);
    }

    /// <summary>The visual tree, skipping collapsed, hidden and fully transparent parts.</summary>
    private static IEnumerable<DependencyObject> Visible(DependencyObject parent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is UIElement { Visibility: not Visibility.Visible } or UIElement { Opacity: <= 0 })
            {
                continue;
            }

            yield return child;
            foreach (var descendant in Visible(child))
            {
                yield return descendant;
            }
        }
    }

    private static string? Find(Control control, FrameworkElement root, Rect window, bool strict)
    {
        var own = Bounds(control, root);
        if (own.Width < 1 && own.Height < 1)
        {
            return control.DesiredSize.Width > 1 || control.DesiredSize.Height > 1 ? "has no room (0 px)" : null;
        }

        // Arranged smaller than it needs: WPF cuts the control itself.
        if (LayoutInformation.GetLayoutClip(control) is { } ownClip && !ownClip.Bounds.IsEmpty
            && (ownClip.Bounds.Width + Slack < control.RenderSize.Width || ownClip.Bounds.Height + Slack < control.RenderSize.Height))
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"cut to {ownClip.Bounds.Width:0}x{ownClip.Bounds.Height:0} of {control.RenderSize.Width:0}x{control.RenderSize.Height:0} (its cell is too small)");
        }

        var subject = own;
        DependencyObject current = control;
        while (!ReferenceEquals(current, root) && VisualTreeHelper.GetParent(current) is { } parent)
        {
            current = parent;
            if (current is ScrollContentPresenter { TemplatedParent: ScrollViewer scroller } presenter)
            {
                var view = Bounds(presenter, root);
                var scrollsX = scroller.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
                var scrollsY = scroller.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;
                var name = Describe(scroller, null);
                if ((strict || !scrollsX) && (subject.Left < view.Left - Slack || subject.Right > view.Right + Slack))
                {
                    return strict && scrollsX
                        ? $"primary button outside the visible part of {name} (needs scrolling sideways)"
                        : $"cut off sideways by {name} (it does not scroll sideways)";
                }

                if ((strict || !scrollsY) && (subject.Top < view.Top - Slack || subject.Bottom > view.Bottom + Slack))
                {
                    return strict && scrollsY
                        ? $"primary button outside the visible part of {name} (needs scrolling; dock it outside the scrolling area)"
                        : $"cut off by {name} (it does not scroll up and down)";
                }

                // Reachable by scrolling: from here on the viewport itself has to be visible.
                subject = new Rect(
                    new Point(scrollsX ? view.Left : subject.Left, scrollsY ? view.Top : subject.Top),
                    new Point(scrollsX ? view.Right : subject.Right, scrollsY ? view.Bottom : subject.Bottom));
                continue;
            }

            if (current is UIElement element && ClipOf(element) is { } clip)
            {
                var visible = element.TransformToAncestor(root).TransformBounds(clip);
                if (!Contains(visible, subject))
                {
                    return $"cut off by {Describe(element, null)} at {Format(visible)}";
                }
            }
        }

        return Contains(window, subject) ? null : $"outside the window ({Format(window)})";
    }

    /// <summary>The area an element shows its children in, when it cuts them (ClipToBounds, Clip, or a layout clip).</summary>
    private static Rect? ClipOf(UIElement element)
    {
        Rect? clip = null;
        if (element.ClipToBounds)
        {
            clip = new Rect(element.RenderSize);
        }

        if (element.Clip is { } geometry && !geometry.Bounds.IsEmpty)
        {
            clip = clip is { } c ? Rect.Intersect(c, geometry.Bounds) : geometry.Bounds;
        }

        if (element is FrameworkElement framework && LayoutInformation.GetLayoutClip(framework) is { } layout && !layout.Bounds.IsEmpty)
        {
            clip = clip is { } c ? Rect.Intersect(c, layout.Bounds) : layout.Bounds;
        }

        return clip;
    }

    private static Rect Bounds(Visual element, Visual root)
    {
        var size = element is UIElement e ? e.RenderSize : default;
        return element.TransformToAncestor(root).TransformBounds(new Rect(size));
    }

    private static bool Contains(Rect outer, Rect inner) =>
        !outer.IsEmpty
        && inner.Left >= outer.Left - Slack && inner.Top >= outer.Top - Slack
        && inner.Right <= outer.Right + Slack && inner.Bottom <= outer.Bottom + Slack;

    /// <summary>A list row or tile (a container of an ItemsControl): not held to the primary-button rule.</summary>
    private static bool IsInList(DependencyObject control, DependencyObject root)
    {
        for (DependencyObject? current = control; current is not null && !ReferenceEquals(current, root); current = VisualTreeHelper.GetParent(current))
        {
            if (ItemsControl.ItemsControlFromItemContainer(current) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static string? PrimaryKey(Style? style, List<(string Key, Style? Style)> primaryStyles)
    {
        for (var s = style; s is not null; s = s.BasedOn)
        {
            foreach (var (key, primaryStyle) in primaryStyles)
            {
                if (ReferenceEquals(s, primaryStyle))
                {
                    return key;
                }
            }
        }

        return null;
    }

    private static string Describe(DependencyObject element, string? styleKey)
    {
        var type = element.GetType().Name;
        var name = element is FrameworkElement { Name: { Length: > 0 } n } ? n : null;
        var label = AutomationProperties.GetName(element) is { Length: > 0 } a ? a
            : element switch
            {
                ContentControl { Content: string s } => s,
                ContentControl { Content: TextBlock { Text: var t } } => t,
                TextBox { Text: { Length: > 0 } t } => t,
                _ => null,
            };
        var parts = new List<string> { type };
        if (label is { Length: > 0 })
        {
            parts.Add($"\"{(label.Length > 40 ? label[..40] + "…" : label)}\"");
        }

        if (name is not null)
        {
            parts.Add($"#{name}");
        }

        if (styleKey is not null)
        {
            parts.Add($"[{styleKey}]");
        }

        return string.Join(' ', parts);
    }

    private static string Format(Rect rect) =>
        string.Create(CultureInfo.InvariantCulture, $"{rect.X:0},{rect.Y:0} {rect.Width:0}x{rect.Height:0}");
}
