using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Gym.UI;

/// <summary>
/// The language the UI shows now. The shell sets it (EN / عربي toggle, station.json); everything bound with
/// <see cref="LocExtension"/> updates at once. Pages get <see cref="Shell.IShellPage.OnLanguageChanged"/> for
/// text they set from code.
/// </summary>
public sealed class UiLanguage : INotifyPropertyChanged
{
    private AppLanguage language;

    private UiLanguage()
    {
    }

    public static UiLanguage Instance { get; } = new();

    public static AppLanguage Current
    {
        get => Instance.language;
        set => Instance.Language = value;
    }

    /// <summary>Raised after the language changed (for app-wide code; pages use OnLanguageChanged instead).</summary>
    public static event EventHandler? Changed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppLanguage Language
    {
        get => language;
        set
        {
            if (language == value)
            {
                return;
            }

            language = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowDirection)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsArabic)));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public FlowDirection FlowDirection => language.ToFlowDirection();

    public bool IsArabic => language == AppLanguage.Arabic;
}

/// <summary>
/// <c>{ui:Loc {x:Static local:MyText.Title}}</c>: shows a <see cref="LocalText"/> in the current language and
/// follows the EN / عربي switch.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(object text) => Text = text;

    /// <summary>A <see cref="LocalText"/> (usually from <c>x:Static</c>).</summary>
    [ConstructorArgument("text")]
    public object? Text { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(UiLanguage.Language))
        {
            Source = UiLanguage.Instance,
            Mode = BindingMode.OneWay,
            Converter = LocalTextConverter.Instance,
            ConverterParameter = Text,
        };
        return binding.ProvideValue(serviceProvider);
    }
}

/// <summary>Turns the language into the parameter's text. Used by <see cref="LocExtension"/>.</summary>
public sealed class LocalTextConverter : IValueConverter
{
    public static LocalTextConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var language = value is AppLanguage l ? l : UiLanguage.Current;
        return parameter switch
        {
            LocalText text => text.For(language),
            null => "",
            _ => parameter.ToString() ?? "",
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
