using System.Windows;
using System.Windows.Controls;
using Gym.UI;
using Gym.UI.Controls;

namespace Gym.Admin.Controls;

/// <summary>An item of a ComboBox built in code: shows <see cref="Text"/>, keeps <see cref="Value"/>.</summary>
public sealed record Choice(object? Value, string Text)
{
    public override string ToString() => Text;
}

/// <summary>
/// Small builders for the dialog forms (labelled inputs from the shared theme), so each dialog is a few lines of code.
/// Labels are set once in the current language: dialogs are short-lived.
/// </summary>
internal static class Forms
{
    public const double Gap = 14;

    public static (FormField Field, TextBox Box) Text(
        LocalText label, string? value = null, bool leftToRight = false, LocalText? hint = null, string? placeholder = null, int maxLength = 0)
    {
        var box = new TextBox { Text = value ?? "", MaxLength = maxLength };
        if (leftToRight)
        {
            box.FlowDirection = FlowDirection.LeftToRight;
        }

        if (placeholder is not null)
        {
            ThemeAssist.SetPlaceholder(box, placeholder);
        }

        return (Field(label, box, hint), box);
    }

    public static (FormField Field, TextBox Box) MultiLine(LocalText label, string? value, int maxLength = 0)
    {
        var box = new TextBox
        {
            Text = value ?? "",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 88,
            MaxLength = maxLength,
            VerticalContentAlignment = VerticalAlignment.Top,
        };
        return (Field(label, box, null), box);
    }

    public static (FormField Field, PasswordBox Box) Password(LocalText label, LocalText? hint = null)
    {
        var box = new PasswordBox();
        return (Field(label, box, hint), box);
    }

    public static (FormField Field, ComboBox Box) Pick(LocalText label, IEnumerable<Choice> items, object? selected, LocalText? hint = null)
    {
        var list = items.ToList();
        var box = new ComboBox { ItemsSource = list };
        box.SelectedItem = list.FirstOrDefault(c => Equals(c.Value, selected)) ?? list.FirstOrDefault();
        return (Field(label, box, hint), box);
    }

    public static (FormField Field, DatePicker Box) Date(LocalText label, DateOnly? value)
    {
        var box = new DatePicker { SelectedDate = value?.ToDateTime(TimeOnly.MinValue) };
        ThemeAssist.SetPlaceholder(box, CommonText.PickDate.Current);
        return (Field(label, box, null), box);
    }

    public static CheckBox Check(LocalText text, bool value) => new() { Content = text.Current, IsChecked = value };

    /// <summary>The value of the selected <see cref="Choice"/>.</summary>
    public static T? Selected<T>(ComboBox box) => box.SelectedItem is Choice { Value: T value } ? value : default;

    public static FormField Field(LocalText label, UIElement input, LocalText? hint)
    {
        var field = new FormField { Label = label.Current, Content = input };
        if (hint is { } text)
        {
            field.Hint = text.Current;
        }

        return field;
    }

    /// <summary>Elements one under the other with the form gap.</summary>
    public static StackPanel Stack(params UIElement[] children)
    {
        var panel = new StackPanel();
        for (var i = 0; i < children.Length; i++)
        {
            if (children[i] is FrameworkElement element && i > 0)
            {
                element.Margin = new Thickness(0, Gap, 0, 0);
            }

            panel.Children.Add(children[i]);
        }

        return panel;
    }

    /// <summary>Elements side by side in equal columns.</summary>
    public static Grid Columns(params UIElement[] children)
    {
        var grid = new Grid();
        for (var i = 0; i < children.Length; i++)
        {
            if (i > 0)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Gap) });
            }

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(children[i], i * 2);
            grid.Children.Add(children[i]);
        }

        return grid;
    }

    /// <summary>A short wrapped note (caption style), e.g. what saving will change.</summary>
    public static TextBlock Note(string text)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.None };
        block.SetResourceReference(FrameworkElement.StyleProperty, "Text.Caption");
        return block;
    }
}
