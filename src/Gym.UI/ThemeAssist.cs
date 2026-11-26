using System.Windows;

namespace Gym.UI;

/// <summary>Attached properties used by the TITANFIT control templates.</summary>
public static class ThemeAssist
{
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.RegisterAttached(
        "CornerRadius", typeof(CornerRadius), typeof(ThemeAssist), new FrameworkPropertyMetadata(new CornerRadius(12)));

    public static CornerRadius GetCornerRadius(DependencyObject element) => (CornerRadius)element.GetValue(CornerRadiusProperty);

    public static void SetCornerRadius(DependencyObject element, CornerRadius value) => element.SetValue(CornerRadiusProperty, value);
}
