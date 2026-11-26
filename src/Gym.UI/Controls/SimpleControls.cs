using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Gym.UI.Shell;

namespace Gym.UI.Controls;

/// <summary>"Nothing here" in a list or a screen: an icon, a title, a line of text and optional content (a button).</summary>
public sealed class EmptyState : ContentControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(Geometry), typeof(EmptyState), new FrameworkPropertyMetadata(UiIcons.Search));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(EmptyState), new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(EmptyState), new FrameworkPropertyMetadata(null));

    static EmptyState()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(EmptyState), new FrameworkPropertyMetadata(typeof(EmptyState)));
    }

    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}

/// <summary>A dimmed layer with a spinner over its parent while <see cref="IsActive"/> (respects reduced motion).</summary>
public sealed class LoadingOverlay : Control
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(LoadingOverlay), new FrameworkPropertyMetadata(false, OnIsActiveChanged));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(LoadingOverlay), new FrameworkPropertyMetadata(null));

    private Path? spinner;

    static LoadingOverlay()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(LoadingOverlay), new FrameworkPropertyMetadata(typeof(LoadingOverlay)));
    }

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        spinner = GetTemplateChild("PART_Spinner") as Path;
        Animate();
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((LoadingOverlay)d).Animate();

    private void Animate()
    {
        if (spinner is null)
        {
            return;
        }

        var rotate = new RotateTransform();
        spinner.RenderTransform = rotate;
        if (IsActive && SystemParameters.ClientAreaAnimation)
        {
            rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.9)) { RepeatBehavior = RepeatBehavior.Forever });
        }
    }
}

/// <summary>
/// A labelled form input with an optional hint or error under it:
/// <c>&lt;controls:FormField Label="{ui:Loc {x:Static local:T.Phone}}"&gt;&lt;TextBox /&gt;&lt;/controls:FormField&gt;</c>.
/// </summary>
public sealed class FormField : ContentControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(FormField), new FrameworkPropertyMetadata(null, OnTextChanged));

    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(
        nameof(Hint), typeof(string), typeof(FormField), new FrameworkPropertyMetadata(null, OnTextChanged));

    public static readonly DependencyProperty ErrorProperty = DependencyProperty.Register(
        nameof(Error), typeof(string), typeof(FormField), new FrameworkPropertyMetadata(null, OnTextChanged));

    private static readonly DependencyPropertyKey HasLabelPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasLabel), typeof(bool), typeof(FormField), new FrameworkPropertyMetadata(false));

    private static readonly DependencyPropertyKey HasHintPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasHint), typeof(bool), typeof(FormField), new FrameworkPropertyMetadata(false));

    private static readonly DependencyPropertyKey HasErrorPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasError), typeof(bool), typeof(FormField), new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty HasLabelProperty = HasLabelPropertyKey.DependencyProperty;

    public static readonly DependencyProperty HasHintProperty = HasHintPropertyKey.DependencyProperty;

    public static readonly DependencyProperty HasErrorProperty = HasErrorPropertyKey.DependencyProperty;

    static FormField()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(FormField), new FrameworkPropertyMetadata(typeof(FormField)));
        FocusableProperty.OverrideMetadata(typeof(FormField), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(FormField), new FrameworkPropertyMetadata(false));
    }

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Hint
    {
        get => (string?)GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    /// <summary>Red text under the input; also gives the input a red border. Null or empty = no error.</summary>
    public string? Error
    {
        get => (string?)GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public bool HasLabel => (bool)GetValue(HasLabelProperty);

    public bool HasHint => (bool)GetValue(HasHintProperty);

    public bool HasError => (bool)GetValue(HasErrorProperty);

    protected override void OnContentChanged(object? oldContent, object? newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (oldContent is DependencyObject old)
        {
            old.ClearValue(ThemeAssist.HasErrorProperty);
        }

        UpdateError();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var field = (FormField)d;
        field.SetValue(HasLabelPropertyKey, !string.IsNullOrEmpty(field.Label));
        field.SetValue(HasHintPropertyKey, !string.IsNullOrEmpty(field.Hint));
        field.SetValue(HasErrorPropertyKey, !string.IsNullOrEmpty(field.Error));
        field.UpdateError();
    }

    private void UpdateError()
    {
        if (Content is DependencyObject input)
        {
            ThemeAssist.SetHasError(input, HasError);
        }
    }
}

/// <summary>A member photo with rounded corners, or a person icon when there is none.</summary>
public sealed class Avatar : Control
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(ImageSource), typeof(Avatar), new FrameworkPropertyMetadata(null, OnSourceChanged));

    private static readonly DependencyPropertyKey HasPhotoPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasPhoto), typeof(bool), typeof(Avatar), new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty HasPhotoProperty = HasPhotoPropertyKey.DependencyProperty;

    static Avatar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(Avatar), new FrameworkPropertyMetadata(typeof(Avatar)));
    }

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool HasPhoto => (bool)GetValue(HasPhotoProperty);

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        d.SetValue(HasPhotoPropertyKey, e.NewValue is not null);
}

/// <summary>The placeholder of a screen that is not built yet: icon, title, text and a detail line (e.g. sample data).</summary>
public sealed class PagePlaceholder : ContentControl
{
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(Geometry), typeof(PagePlaceholder), new FrameworkPropertyMetadata(UiIcons.Dashboard));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PagePlaceholder), new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(PagePlaceholder), new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail), typeof(string), typeof(PagePlaceholder), new FrameworkPropertyMetadata(null, OnDetailChanged));

    private static readonly DependencyPropertyKey HasDetailPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasDetail), typeof(bool), typeof(PagePlaceholder), new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty HasDetailProperty = HasDetailPropertyKey.DependencyProperty;

    static PagePlaceholder()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(PagePlaceholder), new FrameworkPropertyMetadata(typeof(PagePlaceholder)));
    }

    public Geometry? Icon
    {
        get => (Geometry?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Detail
    {
        get => (string?)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    public bool HasDetail => (bool)GetValue(HasDetailProperty);

    private static void OnDetailChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        d.SetValue(HasDetailPropertyKey, e.NewValue is string { Length: > 0 });
}

/// <summary>One toast message (see <see cref="IShellUi.Toast(LocalText, ToastKind)"/>).</summary>
public sealed class ToastView : Control
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(ToastKind), typeof(ToastView), new FrameworkPropertyMetadata(ToastKind.Info));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(ToastView), new FrameworkPropertyMetadata(null));

    static ToastView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ToastView), new FrameworkPropertyMetadata(typeof(ToastView)));
    }

    public ToastKind Kind
    {
        get => (ToastKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }
}

/// <summary>The card of a dialog: title, message, content and the buttons (<see cref="Footer"/>).</summary>
public sealed class DialogFrame : ContentControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(DialogFrame), new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message), typeof(string), typeof(DialogFrame), new FrameworkPropertyMetadata(null, OnMessageChanged));

    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer), typeof(object), typeof(DialogFrame), new FrameworkPropertyMetadata(null));

    private static readonly DependencyPropertyKey HasMessagePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasMessage), typeof(bool), typeof(DialogFrame), new FrameworkPropertyMetadata(false));

    public static readonly DependencyProperty HasMessageProperty = HasMessagePropertyKey.DependencyProperty;

    static DialogFrame()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DialogFrame), new FrameworkPropertyMetadata(typeof(DialogFrame)));
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public bool HasMessage => (bool)GetValue(HasMessageProperty);

    private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        d.SetValue(HasMessagePropertyKey, e.NewValue is string { Length: > 0 });
}

/// <summary>Image helpers for photos from the API.</summary>
public static class Images
{
    /// <summary>A frozen image from PNG/JPEG bytes, or null for no bytes or a broken image.</summary>
    public static BitmapImage? FromBytes(byte[]? bytes, int decodeWidth = 0)
    {
        if (bytes is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            using (var stream = new System.IO.MemoryStream(bytes))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                if (decodeWidth > 0)
                {
                    image.DecodePixelWidth = decodeWidth;
                }

                image.StreamSource = stream;
                image.EndInit();
            }

            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or System.IO.IOException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
