using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Gym.Client;
using Gym.Client.Sample;
using Gym.Contracts;
using Gym.Domain;
using Gym.Reception.Hardware;
using Gym.Reception.Logic;
using Gym.Reception.Views;
using Gym.UI;
using Gym.UI.Controls;
using Gym.UI.Shell;

namespace Gym.Reception.Pages;

/// <summary>
/// New member: ID scan (USB 2D scanner in keyboard mode, read by <see cref="IIdCardParser"/>), phone, webcam photo,
/// member type and plan, then Save creates the member with the first payment (and the private training service for a
/// private trainer) and shows the member QR. The raw scan text stays in the scan box only; nothing of it is stored.
/// </summary>
public partial class NewMemberPage : PageView
{
    /// <summary>A made-up scan for the smoke screenshots (no real person).</summary>
    internal const string SampleScan = "نور|خالد|المصري|هالة|14/03/1999|دمشق|01234567890";

    private readonly IIdCardParser parser = new TolerantIdCardParser();
    private readonly DispatcherTimer scanTimer;
    private readonly PhoneInput phone;
    private IReadOnlyList<PlanDto> plans = [];
    private PlanDto? selectedPlan;
    private long serviceFee;
    private byte[]? photo;
    private MemberDto? saved;
    private IdCardFields? lastScan;
    private bool cameraOwned;

    public NewMemberPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        phone = new PhoneInput(null);
        PhoneHost.Content = phone;
        QrView.Ui = Context.Ui;
        scanTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        scanTimer.Tick += (_, _) =>
        {
            scanTimer.Stop();
            ApplyScan(ScanBox.Text);
        };
        Unloaded += (_, _) => StopCamera();
        ShowPhoto();
        ShowScanStatus();
    }

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        plans = await Context.Api.GetPlansAsync(ct: cancellationToken).ConfigureAwait(true);
        serviceFee = (await Context.Api.GetSettingsAsync(cancellationToken).ConfigureAwait(true)).PrivateTrainingMonthlyFee;
        selectedPlan = plans.FirstOrDefault(p => p.Id == selectedPlan?.Id) ?? plans.FirstOrDefault(p => p.Months == 1) ?? plans.FirstOrDefault();
        BuildPlanChoices();
        UpdateSummary();

        if (Context.IsSample)
        {
            PhotoCamera.ShowSample();
        }
        else if (photo is null && saved is null)
        {
            StartCamera();
        }

        if (saved is null && ScanBox.Text.Length == 0)
        {
            ScanBox.Focus();
        }
    }

    public override void OnLanguageChanged()
    {
        phone.ApplyLanguage();
        PhotoCamera.ApplyLanguage();
        BuildPlanChoices();
        UpdateSummary();
        ShowPhoto();
        ShowScanStatus();
        ShowBirthDateHint();
        if (saved is not null)
        {
            ShowSaved();
        }
    }

    /// <summary>Smoke screenshots: the form filled in from a sample scan, with a phone, a photo and the 3-month plan.</summary>
    internal Task FillSampleAsync()
    {
        ScanBox.Text = SampleScan;
        scanTimer.Stop();
        ApplyScan(ScanBox.Text);
        phone.Box.Text = "944 765 210";
        photo = PlaceholderPhoto.Create(7, 320);
        ShowPhoto();
        if (plans.FirstOrDefault(p => p.Months == 3) is { } threeMonths)
        {
            selectedPlan = threeMonths;
            BuildPlanChoices();
        }

        UpdateSummary();
        return Task.CompletedTask;
    }

    /// <summary>Smoke screenshots: saves the filled-in sample form (creates the member in the sample data).</summary>
    internal Task SaveSampleAsync() => SaveAsync();

    // ID scan

    private void OnScanTextChanged(object sender, TextChangedEventArgs e)
    {
        scanTimer.Stop();
        if (ScanBox.Text.Length > 0)
        {
            scanTimer.Start();
        }
    }

    private void OnRescanClick(object sender, RoutedEventArgs e)
    {
        ScanBox.Clear();
        lastScan = null;
        ShowScanStatus();
        ScanBox.Focus();
    }

    /// <summary>Fills the fields found in the scan; fields it does not find keep what is typed.</summary>
    private void ApplyScan(string raw)
    {
        var fields = parser.Parse(raw);
        lastScan = fields;
        Fill(FirstBox, fields.FirstName);
        Fill(FatherBox, fields.FatherName);
        Fill(LastBox, fields.LastName);
        Fill(MotherBox, fields.MotherName);
        Fill(BirthPlaceBox, fields.BirthPlace);
        Fill(NationalNoBox, fields.NationalNo);
        if (fields.BirthDate is { } birth)
        {
            BirthDateBox.Text = birth.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        ShowScanStatus();
    }

    private static void Fill(TextBox box, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            box.Text = value;
        }
    }

    private void ShowScanStatus()
    {
        var language = Context.Language;
        var (text, icon, brush) = lastScan switch
        {
            null => (ReceptionText.ScanHint.For(language), UiIcons.Info, "Brush.TextSecondary"),
            { IsEmpty: true } => (ReceptionText.ScanNotRecognised.For(language), UiIcons.Alert, "Brush.Warning"),
            { } read => (ReceptionText.ScanRead.Format(language, read.Count), UiIcons.Check, "Brush.Good"),
        };
        ScanStatus.Text = BidiText.FixNumbers(text, language);
        ScanStatusIcon.Data = icon;
        ScanStatusIcon.SetResourceReference(Icon.ForegroundProperty, brush);
    }

    private void OnBirthDateChanged(object sender, TextChangedEventArgs e) => ShowBirthDateHint();

    private void ShowBirthDateHint()
    {
        var language = Context.Language;
        var text = BirthDateBox.Text.Trim();
        BirthDateField.Error = null;
        BirthDateField.Hint = text.Length > 0 && TolerantIdCardParser.ReadDate(text) is { } date ? DateText.ShortDate(date, language) : null;
    }

    // Photo

    private void StartCamera()
    {
        CameraFeed.Shared.Acquire(this, Context.Station.CameraName, decodeQr: false);
        PhotoCamera.Attach(CameraFeed.Shared);
        cameraOwned = true;
    }

    private void StopCamera()
    {
        if (cameraOwned)
        {
            PhotoCamera.Detach();
            CameraFeed.Shared.Release(this);
            cameraOwned = false;
        }
    }

    private void OnPhotoClick(object sender, RoutedEventArgs e)
    {
        if (photo is not null)
        {
            photo = null;
            ShowPhoto();
            if (!Context.IsSample)
            {
                StartCamera();
            }

            return;
        }

        var jpeg = Context.IsSample ? null : CameraFeed.Shared.CaptureSquareJpeg();
        if (jpeg is null)
        {
            PhotoStatus.Text = ReceptionText.NoCameraPicture.For(Context.Language);
            return;
        }

        photo = jpeg;
        StopCamera();
        ShowPhoto();
    }

    private void ShowPhoto()
    {
        var language = Context.Language;
        PhotoImage.Source = Images.FromBytes(photo);
        PhotoFrame.Visibility = photo is null ? Visibility.Collapsed : Visibility.Visible;
        PhotoButton.Content = (photo is null ? ReceptionText.TakePhoto : ReceptionText.Retake).For(language);
        PhotoButton.SetResourceReference(StyleProperty, photo is null ? "Button.Primary" : "Button.Secondary");
        PhotoStatus.Text = photo is null ? ReceptionText.PhotoHint.For(language) : ReceptionText.PhotoTaken.For(language);
    }

    // Type and plan

    private void BuildPlanChoices()
    {
        var language = Context.Language;
        var buttons = new List<RadioButton>();
        foreach (var plan in plans)
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = ReceptionFormat.Months(plan.Months, language), FontWeight = FontWeights.SemiBold, FontSize = 15 });
            content.Children.Add(new MoneyText { Amount = plan.Price, FontSize = 14, Margin = new Thickness(0, 4, 0, 0) });
            var button = new RadioButton { Content = content, Margin = new Thickness(0, 0, 8, 8), Tag = plan, IsEnabled = saved is null };
            button.SetResourceReference(StyleProperty, "Choice.Card");
            button.IsChecked = plan.Id == selectedPlan?.Id;
            button.Checked += (_, _) =>
            {
                selectedPlan = plan;
                UpdateSummary();
            };
            buttons.Add(button);
        }

        PlanChoices.ItemsSource = buttons;
    }

    private void OnTypeChecked(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
        {
            UpdateSummary();
        }
    }

    private void OnServiceChanged(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
        {
            UpdateSummary();
        }
    }

    private bool IsTrainer => TrainerChoice?.IsChecked == true;

    private void UpdateSummary()
    {
        if (StartsText is null || ServicePanel is null)
        {
            return; // still loading the XAML
        }

        var language = Context.Language;
        var today = ReceptionDialogs.Today;
        if (selectedPlan is { } plan)
        {
            var preview = RenewPreview.For(null, today, plan.Months);
            StartsText.Text = DateText.ShortDate(preview.Start, language);
            EndsText.Text = DateText.ShortDate(preview.End, language);
        }
        else
        {
            StartsText.Text = "—";
            EndsText.Text = "—";
        }

        var months = selectedPlan?.Months ?? 1;
        var serviceTotal = PrivateTrainingRules.CalculatePrice(months, serviceFee);
        ServicePanel.Visibility = IsTrainer ? Visibility.Visible : Visibility.Collapsed;
        ServiceAmount.Amount = serviceTotal;
        ServiceDetail.Text = BidiText.FixNumbers(
            ReceptionText.ServiceDetail.Format(language, ReceptionFormat.Months(months, language), Money.Format(serviceFee, language)), language);
        TotalText.Amount = (selectedPlan?.Price ?? 0) + (IsTrainer && ServiceCheck.IsChecked == true ? serviceTotal : 0);
    }

    // Save

    private async void OnSaveClick(object sender, RoutedEventArgs e) => await SaveAsync().ConfigureAwait(true);

    private async Task SaveAsync()
    {
        var language = Context.Language;
        FirstField.Error = null;
        LastField.Error = null;
        BirthDateField.Error = null;
        NationalNoField.Error = null;
        SaveError.Visibility = Visibility.Collapsed;

        var first = Clean(FirstBox.Text);
        var last = Clean(LastBox.Text);
        var valid = true;
        if (first.Length == 0)
        {
            FirstField.Error = ReceptionText.Required.For(language);
            valid = false;
        }

        if (last.Length == 0)
        {
            LastField.Error = ReceptionText.Required.For(language);
            valid = false;
        }

        DateOnly? birthDate = null;
        if (Clean(BirthDateBox.Text).Length > 0)
        {
            birthDate = TolerantIdCardParser.ReadDate(BirthDateBox.Text);
            if (birthDate is null)
            {
                BirthDateField.Error = BidiText.FixNumbers(ReceptionText.DateInvalid.For(language), language);
                valid = false;
            }
        }

        if (!valid)
        {
            SaveError.Text = ReceptionText.FixFields.For(language);
            SaveError.Visibility = Visibility.Visible;
            return;
        }

        if (photo is null && !Context.IsSample
            && !await Context.Ui.ConfirmAsync(ReceptionText.NoPhotoTitle, ReceptionText.NoPhotoText, ReceptionText.SaveWithoutPhoto).ConfigureAwait(true))
        {
            return;
        }

        var trainer = IsTrainer;
        var request = new CreateMemberRequest(
            first,
            last,
            Optional(FatherBox.Text),
            Optional(MotherBox.Text),
            birthDate,
            Optional(BirthPlaceBox.Text),
            Optional(NationalNoBox.Text),
            phone.Phone,
            null,
            trainer ? MemberType.PrivateTrainer : MemberType.Regular,
            selectedPlan?.Id);

        MemberDto member;
        try
        {
            member = await Context.Ui.RunBusyAsync(() => Context.Api.CreateMemberAsync(request), ReceptionText.Saving).ConfigureAwait(true);
        }
        catch (GymApiException ex) when (ex.Code == "duplicate_national_no")
        {
            NationalNoField.Error = ReceptionErrors.Describe(ex, language);
            return;
        }
        catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException or TaskCanceledException)
        {
            SaveError.Text = ReceptionErrors.Describe(ex, language);
            SaveError.Visibility = Visibility.Visible;
            return;
        }

        // The member exists now: a failed photo or service payment is reported, not undone.
        if (photo is not null)
        {
            try
            {
                await Context.Api.UploadMemberPhotoAsync(member.Id, photo, "image/jpeg").ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException or TaskCanceledException)
            {
                Context.Ui.Toast(ReceptionText.PhotoUploadFailed.Format(language, ReceptionErrors.Describe(ex, language)), ToastKind.Warning);
            }
        }

        if (trainer && ServiceCheck.IsChecked == true && selectedPlan is { } plan)
        {
            try
            {
                await Context.Api.AddPrivateServiceAsync(member.Id, plan.Months).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException or TaskCanceledException)
            {
                Context.Ui.Toast(ReceptionText.ServicePaymentFailed.Format(language, ReceptionErrors.Describe(ex, language)), ToastKind.Warning);
            }
        }

        saved = member;
        StopCamera();
        ShowSaved();
        Context.Ui.Toast(BidiText.FixNumbers(ReceptionText.MemberSaved.Format(language, member.MemberNoText), language));
    }

    private void ShowSaved()
    {
        if (saved is not { } member)
        {
            return;
        }

        var language = Context.Language;
        QrView.Show(member.QrCode, member.MemberNoText, member.FullName);
        QrWaiting.Visibility = Visibility.Collapsed;
        QrView.Visibility = Visibility.Visible;
        SavePanel.Visibility = Visibility.Collapsed;
        SavedPanel.Visibility = Visibility.Visible;
        SavedText.Text = BidiText.FixNumbers(ReceptionText.MemberSaved.Format(language, member.MemberNoText), language);
        IdColumn.IsEnabled = false;
        TypeChoices.IsEnabled = false;
        PlanChoices.IsEnabled = false;
        ServicePanel.IsEnabled = false;
        PhotoButton.IsEnabled = false;
    }

    private void OnAnotherClick(object sender, RoutedEventArgs e)
    {
        saved = null;
        photo = null;
        lastScan = null;
        foreach (var box in new[] { ScanBox, FirstBox, FatherBox, LastBox, MotherBox, BirthDateBox, BirthPlaceBox, NationalNoBox, phone.Box })
        {
            box.Clear();
        }

        FirstField.Error = null;
        LastField.Error = null;
        NationalNoField.Error = null;
        RegularChoice.IsChecked = true;
        ServiceCheck.IsChecked = true;
        QrWaiting.Visibility = Visibility.Visible;
        QrView.Visibility = Visibility.Collapsed;
        SavePanel.Visibility = Visibility.Visible;
        SavedPanel.Visibility = Visibility.Collapsed;
        IdColumn.IsEnabled = true;
        TypeChoices.IsEnabled = true;
        PlanChoices.IsEnabled = true;
        ServicePanel.IsEnabled = true;
        PhotoButton.IsEnabled = true;
        selectedPlan = plans.FirstOrDefault(p => p.Months == 1) ?? plans.FirstOrDefault();
        BuildPlanChoices();
        UpdateSummary();
        ShowPhoto();
        ShowScanStatus();
        if (!Context.IsSample)
        {
            StartCamera();
        }

        ScanBox.Focus();
    }

    private static string Clean(string? text) => string.Join(' ', (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string? Optional(string? text) => Clean(text) is { Length: > 0 } value ? value : null;
}
