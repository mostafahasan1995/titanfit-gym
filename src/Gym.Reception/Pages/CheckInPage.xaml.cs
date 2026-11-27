using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Gym.Client;
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
/// Check-in. Members are identified by (see <see cref="CheckInSource"/>): the webcam reading their QR, a USB barcode
/// scanner in keyboard mode (a fast burst of keys ending with Enter, caught anywhere on the page), or the member number
/// typed by the staff. The same code within a few seconds checks in only once.
/// </summary>
public partial class CheckInPage : PageView
{
    private const int RecentCount = 8;

    private readonly CheckInDebouncer debouncer = new();
    private readonly KeyboardWedge wedge = new();
    private readonly SemaphoreSlim checkInGate = new(1, 1);
    private readonly ObservableCollection<RecentCheckIn> recent = [];
    private CheckInResultDto? shown;
    private MemberDto? shownMember;
    private SubscriptionDto? shownSubscription;
    private int shownVersion;
    private int admittedToday;
    private bool sampleShown;
    private bool cameraOwned;
    private bool pickerReady;
    private Window? window;

    public CheckInPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        RecentList.ItemsSource = recent;
        Context.Live.CheckInRecorded += checkIn => Dispatcher.InvokeAsync(() => AddRecent(checkIn));
        Context.Live.MemberChanged += change => Dispatcher.InvokeAsync(() => OnMemberChanged(change));
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyTexts();
    }

    /// <summary>How a member was identified at the door. A fingerprint reader can be added here later (new server check-in method).</summary>
    public enum CheckInSource
    {
        Camera,
        BarcodeScanner,
        Typed,
    }

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        await LoadRecentAsync(cancellationToken).ConfigureAwait(true);
        if (Context.IsSample)
        {
            Camera.ShowSample();
            CameraPicker.IsEnabled = false;
            if (!sampleShown)
            {
                sampleShown = true;
                await ShowSampleAsync(SubscriptionStatus.Active).ConfigureAwait(true);
            }

            return;
        }

        StartCamera();
        ManualBox.Focus();
    }

    public override void OnLanguageChanged()
    {
        ApplyTexts();
        Camera.ApplyLanguage();
        foreach (var item in recent)
        {
            item.ApplyLanguage();
        }
    }

    /// <summary>
    /// Sample data (smoke screenshots): checks in a sample member with this status, so the card shows a real result.
    /// </summary>
    internal async Task ShowSampleAsync(SubscriptionStatus status)
    {
        var page = await Context.Api.SearchMembersAsync(status: status, pageSize: 50).ConfigureAwait(true);
        var member = page.Items.FirstOrDefault(m => m.PhotoUrl is not null) ?? page.Items.FirstOrDefault();
        if (member is not null)
        {
            await CheckInAsync(null, member.MemberNo, CheckInSource.Typed, debounce: false).ConfigureAwait(true);
        }
    }

    private void ApplyTexts()
    {
        var language = Context.Language;
        TodayCountText.Text = BidiText.FixNumbers(ReceptionText.TodayCount.Format(language, admittedToday), language);
        RenderCard();
    }

    // Camera

    private void StartCamera()
    {
        var cameras = CameraDevices.List();
        pickerReady = false;
        CameraPicker.ItemsSource = cameras;
        CameraPicker.SelectedIndex = cameras.Count == 0 ? -1 : CameraDevices.IndexOf(cameras, Context.Station.CameraName);
        CameraPicker.IsEnabled = cameras.Count > 1;
        pickerReady = true;

        CameraFeed.Shared.Acquire(this, Context.Station.CameraName, decodeQr: true);
        if (!cameraOwned)
        {
            CameraFeed.Shared.QrCodeFound += OnQrCodeFound;
            cameraOwned = true;
        }

        Camera.Attach(CameraFeed.Shared);
    }

    private void StopCamera()
    {
        if (!cameraOwned)
        {
            return;
        }

        CameraFeed.Shared.QrCodeFound -= OnQrCodeFound;
        Camera.Detach();
        CameraFeed.Shared.Release(this);
        cameraOwned = false;
    }

    private void OnCameraPicked(object sender, SelectionChangedEventArgs e)
    {
        if (!pickerReady || Context.IsSample || CameraPicker.SelectedItem is not string label || label == Context.Station.CameraName)
        {
            return;
        }

        if (!Context.SaveStation(s => s with { CameraName = label }))
        {
            Context.Ui.Toast(ReceptionText.CameraNotSaved, ToastKind.Warning);
        }

        CameraFeed.Shared.Acquire(this, label, decodeQr: true);
        Camera.Attach(CameraFeed.Shared);
    }

    private void OnQrCodeFound(string code)
    {
        if (IsVisible && debouncer.ShouldCheckIn(code, DateTime.UtcNow))
        {
            _ = CheckInAsync(MemberQrCode.Normalize(code), null, CheckInSource.Camera, debounce: false);
        }
    }

    // USB scanner in keyboard mode: a burst of keys ending with Enter, wherever the focus is on this page.

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Context.IsSample)
        {
            return;
        }

        window = Window.GetWindow(this);
        if (window is not null)
        {
            window.PreviewKeyDown += OnWindowPreviewKeyDown;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (window is not null)
        {
            window.PreviewKeyDown -= OnWindowPreviewKeyDown;
            window = null;
        }

        wedge.Reset();
        StopCamera();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsVisible || e.IsRepeat)
        {
            return;
        }

        // Typing in a dialog over the page is not a scan.
        if (Keyboard.FocusedElement is DependencyObject focused && focused is TextBoxBase && !IsAncestorOf(focused))
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Enter)
        {
            if (wedge.Enter(e.Timestamp) is { } scanned)
            {
                e.Handled = true;
                ManualBox.Clear();
                _ = CheckInScannedAsync(scanned);
            }

            return;
        }

        if (key is Key.LeftShift or Key.RightShift or Key.CapsLock)
        {
            return;
        }

        if (KeyboardWedge.CharFor(key, (Keyboard.Modifiers & ModifierKeys.Shift) != 0) is { } character)
        {
            wedge.Press(character, e.Timestamp);
        }
        else
        {
            wedge.Reset();
        }
    }

    private Task CheckInScannedAsync(string scanned)
    {
        var text = scanned.Trim().ToUpperInvariant();
        if (!text.StartsWith(MemberQrCode.Prefix, StringComparison.Ordinal) && MemberNumber.TryParse(text, out var memberNo))
        {
            return CheckInAsync(null, memberNo, CheckInSource.BarcodeScanner, debounce: true);
        }

        return CheckInAsync(MemberQrCode.Normalize(text), null, CheckInSource.BarcodeScanner, debounce: true);
    }

    // Typed member number (or a QR code text)

    private void OnManualKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CheckInTyped();
        }
    }

    private void OnManualClick(object sender, RoutedEventArgs e) => CheckInTyped();

    private void CheckInTyped()
    {
        var text = ManualBox.Text.Trim();
        ManualField.Error = null;
        if (text.Length == 0)
        {
            ManualBox.Focus();
            return;
        }

        var normalized = SearchText.NormalizeDigits(text).ToUpperInvariant();
        if (normalized.StartsWith(MemberQrCode.Prefix, StringComparison.Ordinal))
        {
            ManualBox.Clear();
            _ = CheckInAsync(MemberQrCode.Normalize(normalized), null, CheckInSource.Typed, debounce: false);
        }
        else if (MemberNumber.TryParse(normalized, out var memberNo))
        {
            ManualBox.Clear();
            _ = CheckInAsync(null, memberNo, CheckInSource.Typed, debounce: false);
        }
        else
        {
            ManualField.Error = BidiText.FixNumbers(ReceptionText.ManualInvalid.For(Context.Language), Context.Language);
        }
    }

    // Check-in and the card

    /// <param name="source">Where the code came from. The server logs QR code or member number; a fingerprint reader
    /// would add a source here and a check-in method on the server.</param>
    private async Task CheckInAsync(string? code, int? memberNo, CheckInSource source, bool debounce)
    {
        _ = source;
        if (debounce && !debouncer.ShouldCheckIn(code ?? "#" + memberNo, DateTime.UtcNow))
        {
            return;
        }

        await checkInGate.WaitAsync().ConfigureAwait(true);
        try
        {
            var result = await Context.Api.CheckInAsync(new CheckInRequest(Context.StationName, code, memberNo)).ConfigureAwait(true);
            AddRecent(result);
            if (!Context.IsSample)
            {
                Sounds.Play(CheckInCard.ToneOf(result.Result));
            }

            await ShowResultAsync(result).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException or TaskCanceledException)
        {
            ReceptionErrors.Show(Context, ex);
        }
        finally
        {
            checkInGate.Release();
        }
    }

    private async Task ShowResultAsync(CheckInResultDto result)
    {
        var version = ++shownVersion;
        shown = result;
        shownMember = null;
        shownSubscription = null;
        ResultPhoto.Source = null;
        RenderCard();
        if (result.MemberId is not { } memberId)
        {
            return;
        }

        try
        {
            var photoTask = PhotoCache.GetAsync(Context.Api, result.PhotoUrl);
            var memberTask = Context.Api.GetMemberAsync(memberId);
            var subscriptionsTask = Context.Api.GetMemberSubscriptionsAsync(memberId);
            var photo = await photoTask.ConfigureAwait(true);
            var member = await memberTask.ConfigureAwait(true);
            var subscriptions = await subscriptionsTask.ConfigureAwait(true);
            if (version != shownVersion)
            {
                return;
            }

            ResultPhoto.Source = photo;
            shownMember = member;
            shownSubscription = subscriptions
                .Where(s => s.Kind == SubscriptionKind.Membership)
                .OrderByDescending(s => s.EndDate)
                .FirstOrDefault();
            RenderCard();
        }
        catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException or TaskCanceledException)
        {
            // The card already shows the result; the details are a bonus.
        }
    }

    private void RenderCard()
    {
        if (shown is not { } result)
        {
            ResultPanel.Visibility = Visibility.Collapsed;
            WaitingState.Visibility = Visibility.Visible;
            ResultCard.SetResourceReference(Border.BorderBrushProperty, "Brush.Line");
            return;
        }

        var language = Context.Language;
        var card = CheckInCard.For(result, DateText.Time(result.At), language);
        ResultPanel.Visibility = Visibility.Visible;
        WaitingState.Visibility = Visibility.Collapsed;

        var (color, soft, line, icon) = card.Tone switch
        {
            CardTone.Good => ("Brush.Good", "Brush.GoodSoft", "Brush.GoodLine", UiIcons.Check),
            CardTone.Warning => ("Brush.Warning", "Brush.WarningSoft", "Brush.WarningLine", UiIcons.Clock),
            _ => ("Brush.Bad", "Brush.BadSoft", "Brush.BadLine", UiIcons.Close),
        };
        ResultCard.SetResourceReference(Border.BorderBrushProperty, line);
        ResultStrip.SetResourceReference(Border.BackgroundProperty, soft);
        ResultBadge.SetResourceReference(Border.BackgroundProperty, color);
        ResultHeadline.SetResourceReference(TextBlock.ForegroundProperty, color);
        DaysNumber.SetResourceReference(TextBlock.ForegroundProperty, color);
        UsedBar.SetResourceReference(Border.BackgroundProperty, color);
        ResultIcon.Data = icon;

        ResultHeadline.Text = card.Headline;
        ResultTime.Text = DateText.Time(result.At);
        BidiText.SetText(ResultName, result.FullName ?? ReceptionText.UnknownMemberName.For(language));
        var phone = shownMember?.Phone;
        ResultIdLine.Text = result.MemberNoText is { } number
            ? Parts.IdLine(number, phone, language)
            : BidiText.FixNumbers(ReceptionText.ScannedCodeLine.Format(language, result.Station), language);
        ResultPill.Tone = card.Tone switch
        {
            CardTone.Good => PillTone.Good,
            CardTone.Warning => PillTone.Warning,
            _ => PillTone.Bad,
        };
        ResultPill.Text = card.StatusText;

        PlanText.Text = shownSubscription is { } subscription ? ReceptionFormat.Months(subscription.Months, language) : "—";
        StartText.Text = shownSubscription is { } started ? DateText.ShortDate(started.StartDate, language) : "—";
        EndCaption.Text = (result.Result == CheckInResult.Expired ? ReceptionText.ExpiredOnCaption : ReceptionText.ValidUntilCaption).For(language);
        EndText.Text = result.EndDate is { } end ? DateText.ShortDate(end, language) : "—";

        DaysNumber.Text = card.DaysNumber;
        DaysCaption.Text = card.DaysCaption;
        var hasPeriod = shownSubscription is not null && result.EndDate is not null;
        UsedPanel.Visibility = hasPeriod ? Visibility.Visible : Visibility.Hidden;
        if (shownSubscription is { } period)
        {
            var used = RenewPreview.UsedFraction(period.StartDate, period.EndDate, DateOnly.FromDateTime(DateTime.Today));
            UsedColumn.Width = new GridLength(used, GridUnitType.Star);
            LeftColumn.Width = new GridLength(1 - used, GridUnitType.Star);
            UsedText.Text = ReceptionText.UsedPercent.Format(language, BidiText.Ltr(((int)Math.Round(used * 100)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%"));
        }

        ResultNote.Text = BidiText.FixNumbers(card.Note, language);
        RenewButton.Visibility = card.ShowRenew ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnRenewClick(object sender, RoutedEventArgs e)
    {
        if (shown?.MemberId is not { } memberId)
        {
            return;
        }

        try
        {
            var member = shownMember ?? await Context.Api.GetMemberAsync(memberId).ConfigureAwait(true);
            var renewed = await ReceptionDialogs.RenewAsync(Context, member, ResultPhoto.Source).ConfigureAwait(true);
            if (renewed is not null && member.MemberNo > 0)
            {
                // Paid at the desk: let the member in now.
                await CheckInAsync(null, member.MemberNo, CheckInSource.Typed, debounce: false).ConfigureAwait(true);
            }
        }
        catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException or TaskCanceledException)
        {
            ReceptionErrors.Show(Context, ex);
        }
    }

    // Recent check-ins (this station and the others, live)

    private async Task LoadRecentAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var list = await Context.Api.GetCheckInsAsync(today, today, ct: ct).ConfigureAwait(true);
        admittedToday = list.Count(c => c.Admitted);
        recent.Clear();
        foreach (var checkIn in list.OrderByDescending(c => c.At).Take(RecentCount))
        {
            var item = new RecentCheckIn(checkIn);
            recent.Add(item);
            _ = item.LoadPhotoAsync(Context.Api);
        }

        ApplyTexts();
    }

    private void AddRecent(CheckInResultDto checkIn)
    {
        if (recent.Any(r => r.CheckIn.Id == checkIn.Id))
        {
            return;
        }

        if (checkIn.Admitted && DateOnly.FromDateTime(checkIn.At.ToLocalTime()) == DateOnly.FromDateTime(DateTime.Today))
        {
            admittedToday++;
        }

        var item = new RecentCheckIn(checkIn);
        recent.Insert(0, item);
        while (recent.Count > RecentCount)
        {
            recent.RemoveAt(recent.Count - 1);
        }

        _ = item.LoadPhotoAsync(Context.Api);
        TodayCountText.Text = BidiText.FixNumbers(ReceptionText.TodayCount.Format(Context.Language, admittedToday), Context.Language);
    }

    private void OnMemberChanged(MemberChangedEvent change)
    {
        // Renewed or changed at the other desk: refresh the card when it shows this member.
        if (shown is { MemberId: { } id } current && id == change.MemberId && IsVisible)
        {
            _ = ShowResultAsync(current);
        }
    }

    private async void OnRecentClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: RecentCheckIn item })
        {
            await ShowResultAsync(item.CheckIn).ConfigureAwait(true);
        }
    }

    /// <summary>One tile of the recent check-ins.</summary>
    public sealed class RecentCheckIn(CheckInResultDto checkIn) : ObservableObject
    {
        private ImageSource? photo;
        private string label = "";
        private string time = DateText.Time(checkIn.At);

        public CheckInResultDto CheckIn { get; } = checkIn;

        public string Name { get; } = checkIn.FullName ?? ReceptionText.UnknownMemberName.Current;

        public FlowDirection NameDirection => BidiText.HasRtl(Name) ? UiLanguage.Instance.FlowDirection : FlowDirection.LeftToRight;

        public string Time
        {
            get => time;
            private set => SetProperty(ref time, value);
        }

        public PillTone Tone { get; } = CheckInCard.ToneOf(checkIn.Result) switch
        {
            CardTone.Good => PillTone.Good,
            CardTone.Warning => PillTone.Warning,
            _ => PillTone.Bad,
        };

        public string Label
        {
            get => label.Length > 0 ? label : CheckInCard.ShortLabel(CheckIn.Result).Current;
            private set => SetProperty(ref label, value);
        }

        public ImageSource? Photo
        {
            get => photo;
            private set => SetProperty(ref photo, value);
        }

        public void ApplyLanguage()
        {
            Label = CheckInCard.ShortLabel(CheckIn.Result).Current;
            OnPropertyChanged(nameof(NameDirection));
            Time = DateText.Time(CheckIn.At);
        }

        public async Task LoadPhotoAsync(IGymApi api) => Photo = await PhotoCache.GetAsync(api, CheckIn.PhotoUrl).ConfigureAwait(true);
    }
}
