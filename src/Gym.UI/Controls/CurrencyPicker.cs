using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Gym.Domain;

namespace Gym.UI.Controls;

/// <summary>
/// "Pay in": US dollars or Syrian pounds, two toggle buttons like the language switch.
/// <c>&lt;controls:CurrencyPicker Currency="{Binding Currency}" CurrencyChanged="..." /&gt;</c>. Pounds are offered
/// only while a rate exists (<see cref="ExchangeRates.HasRate"/>); when the rate goes away the choice falls back to
/// dollars. Follows the language switch.
/// </summary>
public sealed class CurrencyPicker : Border
{
    public static readonly DependencyProperty CurrencyProperty = DependencyProperty.Register(
        nameof(Currency), typeof(Currency), typeof(CurrencyPicker),
        new FrameworkPropertyMetadata(Currency.Usd, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrencyChanged));

    private readonly RadioButton usdButton;
    private readonly RadioButton sypButton;
    private bool updating;

    public CurrencyPicker()
    {
        var group = "Currency" + Guid.NewGuid().ToString("N");
        usdButton = NewButton(group);
        sypButton = NewButton(group);
        sypButton.Margin = new Thickness(4, 0, 0, 0);
        var panel = new WrapPanel(); // the two buttons go on two lines in a very narrow place

        panel.Children.Add(usdButton);
        panel.Children.Add(sypButton);
        Child = panel;
        Padding = new Thickness(4);
        CornerRadius = new CornerRadius(12);
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Left;
        SetResourceReference(BackgroundProperty, "Brush.Panel");
        SetResourceReference(BorderBrushProperty, "Brush.Line");

        PropertyChangedEventManager.AddHandler(UiLanguage.Instance, OnSourceChanged, nameof(UiLanguage.Language));
        PropertyChangedEventManager.AddHandler(ExchangeRates.Current, OnSourceChanged, string.Empty);
        Update();
    }

    /// <summary>Raised when the user (or a lost rate) changes the currency.</summary>
    public event EventHandler? CurrencyChanged;

    public Currency Currency
    {
        get => (Currency)GetValue(CurrencyProperty);
        set => SetValue(CurrencyProperty, value);
    }

    private static void OnCurrencyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (CurrencyPicker)d;
        picker.Update();
        picker.CurrencyChanged?.Invoke(picker, EventArgs.Empty);
    }

    private RadioButton NewButton(string group)
    {
        var button = new RadioButton { GroupName = group, Padding = new Thickness(16, 0, 16, 0) };
        button.SetResourceReference(StyleProperty, "Lang.Toggle");
        button.Checked += OnChecked;
        return button;
    }

    private void OnChecked(object sender, RoutedEventArgs e)
    {
        if (!updating)
        {
            Currency = ReferenceEquals(sender, sypButton) ? Currency.Syp : Currency.Usd;
        }
    }

    private void OnSourceChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void Update()
    {
        var language = UiLanguage.Current;
        var hasRate = ExchangeRates.Current.HasRate;
        if (!hasRate && Currency == Currency.Syp)
        {
            Currency = Currency.Usd; // calls Update again
            return;
        }

        updating = true;
        try
        {
            usdButton.Content = CommonText.PayInUsd.For(language);
            sypButton.Content = CommonText.PayInSyp.For(language);
            sypButton.IsEnabled = hasRate;
            sypButton.ToolTip = hasRate ? null : CommonText.NoRateForSyp.For(language);
            ToolTipService.SetShowOnDisabled(sypButton, true);
            usdButton.IsChecked = Currency == Currency.Usd;
            sypButton.IsChecked = Currency == Currency.Syp;
            AutomationProperties.SetName(this, CommonText.PayIn.For(language));
        }
        finally
        {
            updating = false;
        }
    }
}
