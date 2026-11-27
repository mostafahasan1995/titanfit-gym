using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Gym.Reception.Hardware;
using Gym.UI;

namespace Gym.Reception.Views;

public enum CameraMode
{
    /// <summary>Check-in: the scan frame and "Show your QR code".</summary>
    Scan,

    /// <summary>Member photo: the face oval.</summary>
    Photo,
}

/// <summary>Shows the <see cref="CameraFeed"/> picture with its status. The page starts and stops the feed.</summary>
public partial class CameraView : UserControl
{
    private CameraFeed? feed;
    private CameraMode mode = CameraMode.Scan;
    private CameraState state = CameraState.Off;
    private bool sample;

    public CameraView()
    {
        InitializeComponent();
        ApplyMode();
        ApplyLanguage();
    }

    public CameraMode Mode
    {
        get => mode;
        set
        {
            mode = value;
            ApplyMode();
            ApplyLanguage();
        }
    }

    /// <summary>True while the camera sends pictures.</summary>
    public bool IsRunning => state == CameraState.Running;

    /// <summary>Shows the feed's picture and status until <see cref="Detach"/>.</summary>
    public void Attach(CameraFeed cameraFeed)
    {
        ArgumentNullException.ThrowIfNull(cameraFeed);
        if (ReferenceEquals(feed, cameraFeed))
        {
            return;
        }

        Detach();
        feed = cameraFeed;
        sample = false;
        feed.PreviewChanged += OnPreviewChanged;
        feed.StateChanged += OnStateChanged;
        OnPreviewChanged();
        OnStateChanged(feed.State);
    }

    public void Detach()
    {
        if (feed is not null)
        {
            feed.PreviewChanged -= OnPreviewChanged;
            feed.StateChanged -= OnStateChanged;
            feed = null;
        }

        PreviewImage.Source = null;
        StopScanLine();
    }

    /// <summary>The smoke screenshots: no camera, a calm placeholder.</summary>
    public void ShowSample()
    {
        Detach();
        sample = true;
        state = CameraState.Off;
        ApplyLanguage();
    }

    /// <summary>Updates the texts after the language switch.</summary>
    public void ApplyLanguage()
    {
        var language = UiLanguage.Current;
        var camera = BidiText.Ltr(feed?.CameraLabel);
        var (text, brush) = sample
            ? (ReceptionText.CameraSample.For(language), "Brush.TextSecondary")
            : state switch
            {
                CameraState.Starting => (ReceptionText.CameraStarting.For(language), "Brush.Warning"),
                CameraState.Running => (mode == CameraMode.Scan ? ReceptionText.CameraScanning.Format(language, camera) : camera, "Brush.Good"),
                CameraState.NoCamera => (ReceptionText.CameraNone.For(language), "Brush.Bad"),
                CameraState.Failed => (feed?.Error is { Length: > 0 } error
                    ? ReceptionText.CameraFailedWithError.Format(language, error)
                    : ReceptionText.CameraFailed.For(language), "Brush.Bad"),
                _ => (ReceptionText.CameraOff.For(language), "Brush.TextSecondary"),
            };
        StatusText.Text = BidiText.FixNumbers(text, language);
        StatusDot.SetResourceReference(Shape.FillProperty, brush);

        var broken = !sample && state is CameraState.NoCamera or CameraState.Failed;
        if (mode == CameraMode.Scan)
        {
            HintTitle.Text = (broken ? ReceptionText.ScanHintNoCamera : ReceptionText.ScanHintTitle).For(language);
            HintText.Text = (broken ? ReceptionText.ScanHintNoCameraText : ReceptionText.ScanHintText).For(language);
        }
        else
        {
            HintText.Text = (broken ? ReceptionText.PhotoHintNoCamera : ReceptionText.PhotoHint).For(language);
        }
    }

    private void ApplyMode()
    {
        var scan = mode == CameraMode.Scan;
        ScanFrame.Visibility = scan ? Visibility.Visible : Visibility.Collapsed;
        FaceGuide.Visibility = scan ? Visibility.Collapsed : Visibility.Visible;
        HintPanel.Visibility = scan ? Visibility.Visible : Visibility.Collapsed;
        HintText.FontSize = scan ? 14 : 12;
        HintPanel.Margin = scan ? new Thickness(24, 0, 24, 26) : new Thickness(10, 0, 10, 10);
        StatusChip.Margin = scan ? new Thickness(16) : new Thickness(10);
        StatusText.MaxWidth = scan ? 360 : 150;
        Frame.CornerRadius = new CornerRadius(scan ? 20 : 16);
    }

    private void OnPreviewChanged()
    {
        PreviewImage.Source = feed?.Preview;
        var hasPicture = PreviewImage.Source is not null;
        QrPlaceholder.Visibility = hasPicture ? Visibility.Collapsed : Visibility.Visible;
        FacePlaceholder.Visibility = hasPicture ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnStateChanged(CameraState newState)
    {
        state = newState;
        if (newState != CameraState.Running)
        {
            PreviewImage.Source = null;
            OnPreviewChanged();
        }

        if (newState == CameraState.Running && mode == CameraMode.Scan)
        {
            StartScanLine();
        }
        else
        {
            StopScanLine();
        }

        ApplyLanguage();
    }

    // The line moves up and down the frame while scanning (not with reduced motion).
    private void StartScanLine()
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        var animation = new DoubleAnimation(20, 214, TimeSpan.FromSeconds(1.6))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        ScanLineOffset.BeginAnimation(TranslateTransform.YProperty, animation);
    }

    private void StopScanLine() => ScanLineOffset.BeginAnimation(TranslateTransform.YProperty, null);
}
