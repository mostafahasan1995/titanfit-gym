using System.Windows;
using System.Windows.Controls;
using Gym.Tests.Cafeteria;
using Gym.UI;

namespace Gym.Tests.Foundation;

/// <summary>The UI scale, the smoke test screens, the breakpoints and the layout check of the smoke screenshots.</summary>
public sealed class LayoutTests
{
    [Theory]
    [InlineData(1366, 688, 1.0)] // 1366x768 at 100 %: design size
    [InlineData(1093, 534, 1.0)] // 1366x768 at 125 %: never smaller than design size
    [InlineData(1536, 784, 1.0)] // 1920x1080 at 125 %
    [InlineData(1920, 1000, 1.22)] // 1920x1080 at 100 %: larger
    [InlineData(3840, 2080, 1.6)] // 4K at 100 %: capped
    public void The_scale_grows_on_big_windows_only(double width, double height, double expected) =>
        Assert.Equal(expected, UiScale.For(new Size(width, height), UiScale.DefaultReference), 2);

    [Fact]
    public void Reception_grows_earlier_than_the_other_apps()
    {
        var big = new Size(1920, 1000);
        Assert.True(UiScale.For(big, UiScale.TouchReference) > UiScale.For(big, UiScale.DefaultReference));
        Assert.Equal(1.0, UiScale.For(new Size(double.PositiveInfinity, 500), UiScale.TouchReference));
    }

    [Fact]
    public void The_test_screens_are_the_owner_sizes_minus_taskbar_and_title_bar()
    {
        Assert.Equal(
            ["1366x768-100", "1366x768-125", "1920x1080-125", "1920x1080-100"],
            SmokeScreen.All.Select(s => s.Id).ToArray());
        Assert.Equal(new Size(1093, 534), new SmokeScreen(1366, 768, 125).Client);
        Assert.Equal(new Size(1366, 688), new SmokeScreen(1366, 768, 100).Client);
        Assert.Equal(new Size(1536, 784), new SmokeScreen(1920, 1080, 125).Client);
        Assert.Equal(new Size(1920, 1000), new SmokeScreen(1920, 1080, 100).Client);
    }

    [Theory]
    [InlineData("1366x768@125", 1366, 768, 125)]
    [InlineData("1920x1080-100", 1920, 1080, 100)]
    [InlineData("1920x1080", 1920, 1080, 100)]
    [InlineData("1366X768@150%", 1366, 768, 150)]
    public void A_screen_is_read_from_the_command_line(string text, int width, int height, int percent)
    {
        Assert.True(SmokeScreen.TryParse(text, out var screen));
        Assert.Equal(new SmokeScreen(width, height, percent), screen);
    }

    [Theory]
    [InlineData("")]
    [InlineData("big")]
    [InlineData("1366")]
    [InlineData("100x100@125")]
    [InlineData("1366x768@50")]
    public void A_bad_screen_is_refused(string text) => Assert.False(SmokeScreen.TryParse(text, out _));

    [Fact]
    public void The_scale_host_draws_larger_and_sets_the_breakpoints() => Sta.Run(() =>
    {
        var probe = new Border();
        var host = new UiScaleHost(probe) { Reference = UiScale.DefaultReference };

        Layout(host, 1093, 534);
        Assert.Equal(1.0, host.Scale);
        Assert.True(Responsive.GetIsShort(probe));
        Assert.True(Responsive.GetIsVeryShort(probe));
        Assert.True(Responsive.GetIsNarrow(probe));
        Assert.Equal(new Size(1093, 534), probe.RenderSize);

        Layout(host, 1366, 688);
        Assert.True(Responsive.GetIsShort(probe));
        Assert.False(Responsive.GetIsVeryShort(probe));
        Assert.False(Responsive.GetIsNarrow(probe));

        Layout(host, 1920, 1000);
        Assert.Equal(1.22, host.Scale, 2);
        Assert.False(Responsive.GetIsShort(probe));
        Assert.False(Responsive.GetIsNarrow(probe));
        Assert.Equal(1920 / 1.22, probe.RenderSize.Width, 0);
    });

    [Fact]
    public void Collapse_when_short_hides_an_element_on_short_windows_only() => Sta.Run(() =>
    {
        var logo = new Border { Height = 80 };
        Responsive.SetCollapseWhenShort(logo, true);
        var host = new UiScaleHost(new StackPanel { Children = { logo } });

        Layout(host, 1093, 534);
        Assert.Equal(Visibility.Collapsed, logo.Visibility);

        Layout(host, 1366, 900);
        Assert.Equal(Visibility.Visible, logo.Visibility);
    });

    [Fact]
    public void Controls_inside_the_window_pass() => Sta.Run(() =>
    {
        var root = Root(new StackPanel { Children = { new Button { Content = "One", Height = 48 }, new TextBox { Height = 40 } } });

        var report = LayoutAudit.Check(root);

        Assert.Equal(2, report.Checked);
        Assert.Empty(report.Problems);
    });

    [Fact]
    public void A_button_below_the_bottom_of_a_too_small_panel_is_reported() => Sta.Run(() =>
    {
        var root = Root(Buttons(8));

        var report = LayoutAudit.Check(root);

        Assert.Equal(8, report.Checked);
        Assert.NotEmpty(report.Problems);
        Assert.Contains(report.Problems, p => p.Contains("\"Button 8\"", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Problems, p => p.Contains("\"Button 1\"", StringComparison.Ordinal));
    });

    [Fact]
    public void A_button_the_user_can_scroll_to_passes() => Sta.Run(() =>
    {
        var root = Root(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = Buttons(8) });

        var report = LayoutAudit.Check(root);

        Assert.Empty(report.Problems);
    });

    [Fact]
    public void A_scrolling_area_that_does_not_scroll_that_way_cuts_the_button_off() => Sta.Run(() =>
    {
        var root = Root(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = Buttons(8) });

        var report = LayoutAudit.Check(root);

        Assert.Contains(report.Problems, p => p.Contains("\"Button 8\"", StringComparison.Ordinal) && p.Contains("cut off", StringComparison.Ordinal));
    });

    [Fact]
    public void A_primary_button_must_be_in_view_without_scrolling() => Sta.Run(() =>
    {
        var primary = new Style(typeof(Button));
        var panel = Buttons(7);
        panel.Children.Add(new Button { Content = "Save", Height = 48, Style = primary });
        var root = Root(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel });
        root.Resources["Button.Primary"] = primary;

        var report = LayoutAudit.Check(root);

        Assert.Equal(1, report.Primary);
        var problem = Assert.Single(report.Problems);
        Assert.Contains("\"Save\"", problem, StringComparison.Ordinal);
        Assert.Contains("[Button.Primary]", problem, StringComparison.Ordinal);
        Assert.Contains("needs scrolling", problem, StringComparison.Ordinal);
    });

    [Fact]
    public void Primary_buttons_in_list_rows_may_scroll() => Sta.Run(() =>
    {
        var primary = new Style(typeof(Button));
        var list = new ItemsControl { ItemsSource = Enumerable.Range(1, 8).Select(i => new Button { Content = $"Pay {i}", Height = 48, Style = primary }).ToList() };
        var root = Root(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list });
        root.Resources["Button.Primary"] = primary;

        var report = LayoutAudit.Check(root);

        Assert.Equal(8, report.Checked);
        Assert.Equal(0, report.Primary);
        Assert.Empty(report.Problems);
    });

    [Fact]
    public void A_button_squeezed_into_a_too_small_cell_is_reported() => Sta.Run(() =>
    {
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(60) }, new ColumnDefinition() } };
        grid.Children.Add(new Button { Content = "Check in", MinWidth = 140, Height = 48, VerticalAlignment = VerticalAlignment.Top });
        var root = Root(grid);

        var report = LayoutAudit.Check(root);

        Assert.Contains(report.Problems, p => p.Contains("\"Check in\"", StringComparison.Ordinal));
    });

    [Fact]
    public void A_list_squeezed_to_nothing_is_reported() => Sta.Run(() =>
    {
        var grid = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition() } };
        grid.Children.Add(new Border { Height = 190 });
        var list = new ListBox { ItemsSource = new[] { "One", "Two" } };
        Grid.SetRow(list, 1);
        grid.Children.Add(list);
        var root = Root(grid);

        var report = LayoutAudit.Check(root);

        Assert.Contains(report.Problems, p => p.StartsWith("ListBox", StringComparison.Ordinal) && p.Contains("no room for its rows", StringComparison.Ordinal));
    });

    [Fact]
    public void A_table_whose_columns_do_not_fit_is_reported() => Sta.Run(() =>
    {
        var grid = new DataGrid { AutoGenerateColumns = false, HeadersVisibility = DataGridHeadersVisibility.Column };
        ScrollViewer.SetHorizontalScrollBarVisibility(grid, ScrollBarVisibility.Disabled);
        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 120 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Status", Width = new DataGridLength(260) });
        grid.ItemsSource = new[] { "One" };
        var root = Root(grid);

        var report = LayoutAudit.Check(root);

        Assert.Contains(report.Problems, p => p.StartsWith("DataGrid", StringComparison.Ordinal) && p.Contains("columns cut off", StringComparison.Ordinal));
    });

    [Fact]
    public void Weighted_responsive_columns_stack_when_a_column_gets_too_narrow() => Sta.Run(() =>
    {
        var first = new Border { Height = 40 };
        var second = new Border { Height = 30 };
        Gym.UI.Controls.ResponsiveColumns.SetWeight(first, 3);
        var panel = new Gym.UI.Controls.ResponsiveColumns { Columns = 2, MinColumnWidth = 100, Gap = 20, Children = { first, second } };

        Layout(panel, 420, 300);
        Assert.Equal(300, first.RenderSize.Width, 0);
        Assert.Equal(100, second.RenderSize.Width, 0);

        Layout(panel, 300, 300);
        Assert.Equal(300, first.RenderSize.Width, 0);
        Assert.Equal(300, second.RenderSize.Width, 0);
        Assert.Equal(56, second.TranslatePoint(default, panel).Y, 0);
    });

    [Fact]
    public void A_split_row_wraps_its_end_group_below_when_it_does_not_fit() => Sta.Run(() =>
    {
        var start = new Border { Width = 200, Height = 40 };
        var end = new Border { Width = 150, Height = 40 };
        var row = new Gym.UI.Controls.SplitRow { Gap = 10, RowGap = 10, Children = { start, end } };

        Layout(row, 500, 200);
        Assert.Equal(350, end.TranslatePoint(default, row).X, 0);

        Layout(row, 300, 200);
        Assert.Equal(new Point(0, 50), end.TranslatePoint(default, row));
    });

    private static StackPanel Buttons(int count)
    {
        var panel = new StackPanel();
        for (var i = 1; i <= count; i++)
        {
            panel.Children.Add(new Button { Content = $"Button {i}", Height = 48 });
        }

        return panel;
    }

    private static Grid Root(UIElement content)
    {
        var root = new Grid { Width = 300, Height = 200 };
        root.Children.Add(content);
        Layout(root, 300, 200);
        return root;
    }

    private static void Layout(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }
}
