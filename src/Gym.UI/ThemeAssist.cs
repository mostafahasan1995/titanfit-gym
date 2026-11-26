using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Gym.UI;

/// <summary>Attached properties used by the TITANFIT control templates.</summary>
public static class ThemeAssist
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
        "CornerRadius", typeof(CornerRadius), typeof(ThemeAssist), new FrameworkPropertyMetadata(new CornerRadius(12)));

    /// <summary>Grey hint text in an empty TextBox, PasswordBox, ComboBox, DatePicker or SearchBox.</summary>
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.RegisterAttached(
        "Placeholder", typeof(string), typeof(ThemeAssist), new FrameworkPropertyMetadata(null));

    /// <summary>An icon shown before a button's text: <c>ui:ThemeAssist.Icon="{x:Static ui:UiIcons.Check}"</c>.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.RegisterAttached(
        "Icon", typeof(Geometry), typeof(ThemeAssist), new FrameworkPropertyMetadata(null));

    /// <summary>Red border on an input (pair it with <see cref="Controls.FormField.Error"/>).</summary>
    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.RegisterAttached(
        "HasError", typeof(bool), typeof(ThemeAssist), new FrameworkPropertyMetadata(false));

    /// <summary>Kept up to date for every PasswordBox (it has no bindable Password), for the placeholder.</summary>
    public static readonly DependencyProperty HasTextProperty = DependencyProperty.RegisterAttached(
        "HasText", typeof(bool), typeof(ThemeAssist), new FrameworkPropertyMetadata(false));

    static ThemeAssist()
    {
        EventManager.RegisterClassHandler(typeof(PasswordBox), PasswordBox.PasswordChangedEvent, new RoutedEventHandler(OnPasswordChanged));
    }

    /// <summary>Runs the static constructor (the PasswordBox handler) before any window is built.</summary>
    internal static void Initialize()
    {
    }

    public static CornerRadius GetCornerRadius(DependencyObject element) => (CornerRadius)element.GetValue(CornerRadiusProperty);

    public static void SetCornerRadius(DependencyObject element, CornerRadius value) => element.SetValue(CornerRadiusProperty, value);

    public static string? GetPlaceholder(DependencyObject element) => (string?)element.GetValue(PlaceholderProperty);

    public static void SetPlaceholder(DependencyObject element, string? value) => element.SetValue(PlaceholderProperty, value);

    public static Geometry? GetIcon(DependencyObject element) => (Geometry?)element.GetValue(IconProperty);

    public static void SetIcon(DependencyObject element, Geometry? value) => element.SetValue(IconProperty, value);

    public static bool GetHasError(DependencyObject element) => (bool)element.GetValue(HasErrorProperty);

    public static void SetHasError(DependencyObject element, bool value) => element.SetValue(HasErrorProperty, value);

    public static bool GetHasText(DependencyObject element) => (bool)element.GetValue(HasTextProperty);

    public static void SetHasText(DependencyObject element, bool value) => element.SetValue(HasTextProperty, value);

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
        {
            SetHasText(box, box.Password.Length > 0);
        }
    }
}
