using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Gym.Domain;

namespace Gym.UI.Controls;

public enum PillTone
{
    Neutral,
    Info,
    Good,
    Warning,
    Bad,
    Gold,
}

/// <summary>
/// A coloured status label with an icon (never colour alone). Set <see cref="Status"/> for a subscription
/// (Active green / Expiring amber / Expired red / null grey "No subscription", label in the current language):
/// <c>&lt;controls:StatusPill Status="{Binding Membership.Status}" /&gt;</c>. Or leave Status unset and use
/// <see cref="Tone"/> and <see cref="Text"/> for anything else.
/// </summary>
public sealed class StatusPill : Control
{
    private static readonly object NotSet = new();

    /// <summary>A <see cref="SubscriptionStatus"/>, null (no subscription), or the name of a status ("Active").</summary>
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(object), typeof(StatusPill), new FrameworkPropertyMetadata(NotSet, OnInputChanged));

    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone), typeof(PillTone), typeof(StatusPill), new FrameworkPropertyMetadata(PillTone.Neutral, OnInputChanged));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(StatusPill), new FrameworkPropertyMetadata(null, OnInputChanged));

    private static readonly DependencyPropertyKey DisplayTextPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(DisplayText), typeof(string), typeof(StatusPill), new FrameworkPropertyMetadata(""));

    private static readonly DependencyPropertyKey DisplayTonePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(DisplayTone), typeof(PillTone), typeof(StatusPill), new FrameworkPropertyMetadata(PillTone.Neutral));

    private static readonly DependencyPropertyKey IconDataPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IconData), typeof(Geometry), typeof(StatusPill), new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty DisplayTextProperty = DisplayTextPropertyKey.DependencyProperty;

    public static readonly DependencyProperty DisplayToneProperty = DisplayTonePropertyKey.DependencyProperty;

    public static readonly DependencyProperty IconDataProperty = IconDataPropertyKey.DependencyProperty;

    static StatusPill()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(StatusPill), new FrameworkPropertyMetadata(typeof(StatusPill)));
    }

    public StatusPill()
    {
        PropertyChangedEventManager.AddHandler(UiLanguage.Instance, OnLanguageChanged, nameof(UiLanguage.Language));
        Update();
    }

    public SubscriptionStatus? Status
    {
        get => ReadStatus(GetValue(StatusProperty));
        set => SetValue(StatusProperty, value);
    }

    public PillTone Tone
    {
        get => (PillTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <summary>Overrides the label (e.g. "Expires in 2 days").</summary>
    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string DisplayText => (string)GetValue(DisplayTextProperty);

    public PillTone DisplayTone => (PillTone)GetValue(DisplayToneProperty);

    public Geometry? IconData => (Geometry?)GetValue(IconDataProperty);

    /// <summary>Tone, icon and label for a subscription status.</summary>
    public static (PillTone Tone, Geometry Icon, LocalText Label) ForStatus(SubscriptionStatus? status) => status switch
    {
        SubscriptionStatus.Active => (PillTone.Good, UiIcons.Check, CommonText.StatusActive),
        SubscriptionStatus.ExpiringSoon => (PillTone.Warning, UiIcons.Clock, CommonText.StatusExpiring),
        SubscriptionStatus.Expired => (PillTone.Bad, UiIcons.Close, CommonText.StatusExpired),
        _ => (PillTone.Neutral, UiIcons.Minus, CommonText.StatusNone),
    };

    private static SubscriptionStatus? ReadStatus(object? value) => value switch
    {
        SubscriptionStatus status => status,
        string text when Enum.TryParse<SubscriptionStatus>(text, ignoreCase: true, out var parsed) => parsed,
        _ => null,
    };

    private static void OnInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((StatusPill)d).Update();

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void Update()
    {
        var status = GetValue(StatusProperty);
        if (!ReferenceEquals(status, NotSet))
        {
            var (tone, icon, label) = ForStatus(ReadStatus(status));
            SetValue(DisplayTonePropertyKey, tone);
            SetValue(IconDataPropertyKey, icon);
            SetValue(DisplayTextPropertyKey, Text ?? label.Current);
            return;
        }

        SetValue(DisplayTonePropertyKey, Tone);
        SetValue(IconDataPropertyKey, Tone switch
        {
            PillTone.Good => UiIcons.Check,
            PillTone.Warning => UiIcons.Clock,
            PillTone.Bad => UiIcons.Alert,
            PillTone.Gold => UiIcons.Star,
            _ => UiIcons.Info,
        });
        SetValue(DisplayTextPropertyKey, Text ?? "");
    }
}
