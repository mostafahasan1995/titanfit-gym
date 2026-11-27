using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gym.Reception.Logic;
using Gym.UI;
using Gym.UI.Controls;
using Gym.UI.Shell;
using Microsoft.Win32;

namespace Gym.Reception.Views;

/// <summary>The member QR with Copy image, Save PNG and Print. Call <see cref="Show"/> with the member's QR code text.</summary>
public partial class MemberQrView : UserControl
{
    private byte[]? png;
    private string memberNoText = "";
    private string fullName = "";

    public MemberQrView()
    {
        InitializeComponent();
    }

    /// <summary>Side of the QR tile in pixels (148 on the new-member card; bigger in the dialog, easier to photograph).</summary>
    public double QrSize
    {
        get => QrTile.Width;
        set
        {
            QrTile.Width = value;
            QrTile.Height = value;
        }
    }

    /// <summary>For the toasts (copied, saved, errors).</summary>
    public IShellUi? Ui { get; set; }

    /// <summary>The PNG on screen (black on white, 16 pixels per module).</summary>
    public byte[]? Png => png;

    public void Show(string qrCode, string memberNo, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(qrCode);
        png = MemberQr.CreatePng(qrCode);
        QrImage.Source = Images.FromBytes(png);
        memberNoText = memberNo ?? "";
        fullName = name ?? "";
        NumberText.Text = memberNoText;
        BidiText.SetText(NameText, fullName);
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (QrImage.Source is not BitmapSource image)
        {
            return;
        }

        try
        {
            Clipboard.SetImage(image);
            Ui?.Toast(ReceptionText.QrCopied);
        }
        catch (ExternalException)
        {
            Ui?.Toast(ReceptionText.QrCopyFailed, ToastKind.Error);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (png is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            FileName = "TITANFIT-" + memberNoText.TrimStart('#') + ".png",
            Filter = ReceptionText.PngFilter.Current,
            DefaultExt = ".png",
            AddExtension = true,
        };
        var owner = Window.GetWindow(this);
        var chosen = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        if (chosen != true)
        {
            return;
        }

        try
        {
            File.WriteAllBytes(dialog.FileName, png);
            Ui?.Toast(ReceptionText.QrSaved);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Ui?.Toast(ReceptionText.QrSaveFailed.Format(UiLanguage.Current, ex.Message), ToastKind.Error);
        }
    }

    private void OnPrintClick(object sender, RoutedEventArgs e)
    {
        if (QrImage.Source is null)
        {
            return;
        }

        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var page = BuildPrintPage(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
            dialog.PrintVisual(page, "TITANFIT " + memberNoText);
        }
#pragma warning disable CA1031 // Any printer problem is shown to the staff instead of closing the app.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Ui?.Toast(ReceptionText.PrintFailed.Format(UiLanguage.Current, ex.Message), ToastKind.Error);
        }
    }

    // Black on white for paper, whatever the screen theme.
    private Border BuildPrintPage(double width, double height)
    {
        var heading = (FontFamily)FindResource("Font.Heading");
        var body = (FontFamily)FindResource("Font.Body");
        var qr = new Image { Source = QrImage.Source, Width = 300, Height = 300, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 18) };
        RenderOptions.SetBitmapScalingMode(qr, BitmapScalingMode.NearestNeighbor);
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 72, 0, 0) };
        panel.Children.Add(qr);
        panel.Children.Add(new TextBlock
        {
            Text = memberNoText,
            FontFamily = heading,
            FontSize = 30,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            FlowDirection = FlowDirection.LeftToRight,
        });
        var name = new TextBlock
        {
            FontFamily = body,
            FontSize = 20,
            Foreground = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0),
        };
        BidiText.SetText(name, fullName);
        panel.Children.Add(name);
        panel.Children.Add(new TextBlock
        {
            Text = ReceptionText.QrPrintHelp.Current,
            FontFamily = body,
            FontSize = 15,
            Foreground = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 14, 0, 0),
            FlowDirection = UiLanguage.Instance.FlowDirection,
        });
        var page = new Border { Width = width, Height = height, Background = Brushes.White, Child = panel };
        page.Measure(new Size(width, height));
        page.Arrange(new Rect(0, 0, width, height));
        page.UpdateLayout();
        return page;
    }
}
