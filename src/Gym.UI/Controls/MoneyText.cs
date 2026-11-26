using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Gym.UI.Controls;

/// <summary>
/// An amount in Syrian pounds, in gold: <c>&lt;controls:MoneyText Amount="{Binding Total}" /&gt;</c> shows
/// "150,000 SYP" or "150,000 ل.س" (Western digits) and follows the language switch.
/// </summary>
public sealed class MoneyText : TextBlock
{
    public static readonly DependencyProperty AmountProperty = DependencyProperty.Register(
        nameof(Amount), typeof(long), typeof(MoneyText), new FrameworkPropertyMetadata(0L, OnChanged));

    public static readonly DependencyProperty ShowCurrencyProperty = DependencyProperty.Register(
        nameof(ShowCurrency), typeof(bool), typeof(MoneyText), new FrameworkPropertyMetadata(true, OnChanged));

    public MoneyText()
    {
        PropertyChangedEventManager.AddHandler(UiLanguage.Instance, OnLanguageChanged, nameof(UiLanguage.Language));
        Update();
    }

    public long Amount
    {
        get => (long)GetValue(AmountProperty);
        set => SetValue(AmountProperty, value);
    }

    /// <summary>False shows only the number ("150,000").</summary>
    public bool ShowCurrency
    {
        get => (bool)GetValue(ShowCurrencyProperty);
        set => SetValue(ShowCurrencyProperty, value);
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MoneyText)d).Update();

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void Update() => Text = ShowCurrency ? Money.Format(Amount, UiLanguage.Current) : Money.FormatAmount(Amount);
}
