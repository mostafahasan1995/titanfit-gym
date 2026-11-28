using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Gym.UI.Controls;

/// <summary>
/// The Syrian pound equivalent of a dollar amount at the day's rate: <c>&lt;controls:SypText Amount="{Binding Price}" /&gt;</c>
/// shows "230,000 SYP" / "230,000 ل.س" (rounded with the admin's step) and follows the rate and the language. Hidden
/// while there is no rate. Secondary text colour by default.
/// </summary>
public sealed class SypText : TextBlock
{
    public static readonly DependencyProperty AmountProperty = DependencyProperty.Register(
        nameof(Amount), typeof(decimal), typeof(SypText), new FrameworkPropertyMetadata(0m, OnChanged));

    public SypText()
    {
        SetResourceReference(ForegroundProperty, "Brush.TextSecondary");
        PropertyChangedEventManager.AddHandler(UiLanguage.Instance, OnSourceChanged, nameof(UiLanguage.Language));
        PropertyChangedEventManager.AddHandler(ExchangeRates.Current, OnSourceChanged, string.Empty);
        Update();
    }

    /// <summary>US dollars.</summary>
    public decimal Amount
    {
        get => (decimal)GetValue(AmountProperty);
        set => SetValue(AmountProperty, value);
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((SypText)d).Update();

    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void Update()
    {
        if (ExchangeRates.Current.ToSyp(Amount) is { } syp)
        {
            Text = Money.FormatSyp(syp, UiLanguage.Current);
            Visibility = Visibility.Visible;
        }
        else
        {
            Text = "";
            Visibility = Visibility.Collapsed;
        }
    }
}
