using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Gym.Domain;

namespace Gym.UI.Controls;

/// <summary>
/// An amount, in gold: <c>&lt;controls:MoneyText Amount="{Binding Total}" /&gt;</c> shows US dollars ("$12.50"); with
/// <c>Currency="Syp"</c> the amount is Syrian pounds ("150,000 ل.س"). Western digits; follows the language switch.
/// Put a <see cref="SypText"/> next to it for the pounds at the day's rate.
/// </summary>
public sealed class MoneyText : TextBlock
{
    public static readonly DependencyProperty AmountProperty = DependencyProperty.Register(
        nameof(Amount), typeof(decimal), typeof(MoneyText), new FrameworkPropertyMetadata(0m, OnChanged));

    public static readonly DependencyProperty ShowCurrencyProperty = DependencyProperty.Register(
        nameof(ShowCurrency), typeof(bool), typeof(MoneyText), new FrameworkPropertyMetadata(true, OnChanged));

    public static readonly DependencyProperty CurrencyProperty = DependencyProperty.Register(
        nameof(Currency), typeof(Currency), typeof(MoneyText), new FrameworkPropertyMetadata(Currency.Usd, OnChanged));

    public MoneyText()
    {
        PropertyChangedEventManager.AddHandler(UiLanguage.Instance, OnLanguageChanged, nameof(UiLanguage.Language));
        Update();
    }

    /// <summary>US dollars (or pounds with <see cref="Currency"/> = Syp).</summary>
    public decimal Amount
    {
        get => (decimal)GetValue(AmountProperty);
        set => SetValue(AmountProperty, value);
    }

    /// <summary>False shows only the number ("12.50").</summary>
    public bool ShowCurrency
    {
        get => (bool)GetValue(ShowCurrencyProperty);
        set => SetValue(ShowCurrencyProperty, value);
    }

    /// <summary>The currency of <see cref="Amount"/> (default US dollars).</summary>
    public Currency Currency
    {
        get => (Currency)GetValue(CurrencyProperty);
        set => SetValue(CurrencyProperty, value);
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((MoneyText)d).Update();

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void Update() => Text = ShowCurrency ? Money.Format(Currency, Amount, UiLanguage.Current) : Money.FormatAmount(Currency, Amount);
}
