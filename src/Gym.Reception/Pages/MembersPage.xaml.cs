using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
/// Members: search (name, phone, national number, member number, QR code typed or scanned), details with payments and
/// check-ins, renew, show or reissue the QR, change the phone and the photo. Follows live changes from the other desk.
/// </summary>
public partial class MembersPage : PageView
{
    private const int PageSize = 60;

    private readonly KeyboardWedge wedge = new();
    private readonly DispatcherTimer liveRefresh;
    private readonly List<MemberRow> rows = [];
    private SubscriptionStatus? filter;
    private Guid? selectedId;
    private MemberDto? member;
    private ImageSource? memberPhoto;
    private IReadOnlyList<SubscriptionDto> payments = [];
    private IReadOnlyList<CheckInResultDto> visits = [];
    private int total;
    private int searchVersion;
    private int detailVersion;
    private bool scanning;
    private bool sampleSelected;
    private Window? window;

    public MembersPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        liveRefresh = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        liveRefresh.Tick += async (_, _) =>
        {
            liveRefresh.Stop();
            await RefreshAsync().ConfigureAwait(true);
        };
        Context.Live.MemberChanged += _ => Dispatcher.InvokeAsync(OnLiveChange);
        Context.Live.CheckInRecorded += checkIn => Dispatcher.InvokeAsync(() =>
        {
            if (checkIn.MemberId == selectedId)
            {
                OnLiveChange();
            }
        });
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ShowDetails();
    }

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        await SearchAsync(cancellationToken).ConfigureAwait(true);
        if (selectedId is { } id)
        {
            await LoadDetailsAsync(id).ConfigureAwait(true);
        }
    }

    public override void OnLanguageChanged()
    {
        foreach (var row in rows)
        {
            row.ApplyLanguage(Context.Language);
        }

        ShowCount();
        ShowDetails();
        ScanCamera.ApplyLanguage();
    }

    /// <summary>Smoke screenshots: an expired member with the renew dialog open.</summary>
    internal async Task ShowRenewSampleAsync()
    {
        await SelectSampleAsync(SubscriptionStatus.Expired).ConfigureAwait(true);
        if (member is not null)
        {
            _ = ReceptionDialogs.RenewAsync(Context, member, memberPhoto);
        }
    }

    /// <summary>Smoke screenshots: the member QR dialog.</summary>
    internal async Task ShowQrSampleAsync()
    {
        await SelectSampleAsync(SubscriptionStatus.Active).ConfigureAwait(true);
        if (member is not null)
        {
            _ = ReceptionDialogs.ShowQrAsync(Context, member);
        }
    }

    private async Task SelectSampleAsync(SubscriptionStatus status)
    {
        var page = await Context.Api.SearchMembersAsync(status: status, pageSize: 50).ConfigureAwait(true);
        if ((page.Items.FirstOrDefault(m => m.PhotoUrl is not null) ?? page.Items.FirstOrDefault()) is { } pick)
        {
            Search.Text = pick.FullName;
            await SearchAsync(CancellationToken.None).ConfigureAwait(true);
            await LoadDetailsAsync(pick.Id).ConfigureAwait(true);
        }
    }

    // Search

    private async void OnSearchChanged(object? sender, EventArgs e) => await SearchAsync(CancellationToken.None).ConfigureAwait(true);

    private async void OnFilterChecked(object sender, RoutedEventArgs e)
    {
        filter = sender switch
        {
            _ when ReferenceEquals(sender, FilterActive) => SubscriptionStatus.Active,
            _ when ReferenceEquals(sender, FilterExpiring) => SubscriptionStatus.ExpiringSoon,
            _ when ReferenceEquals(sender, FilterExpired) => SubscriptionStatus.Expired,
            _ => null,
        };
        if (IsLoaded)
        {
            await SearchAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async Task SearchAsync(CancellationToken ct)
    {
        var mine = ++searchVersion;
        PagedResult<MemberSummary> page;
        try
        {
            page = await Context.Api.SearchMembersAsync(Search.Text, filter, null, 1, PageSize, ct).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException)
        {
            ReceptionErrors.Show(Context, ex);
            return;
        }

        if (mine != searchVersion)
        {
            return;
        }

        rows.Clear();
        rows.AddRange(page.Items.Select(m => new MemberRow(m, Context.Language)));
        total = page.Total;
        MembersList.ItemsSource = null;
        MembersList.ItemsSource = rows;
        NoResults.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowCount();
        foreach (var row in rows)
        {
            _ = row.LoadPhotoAsync(Context.Api);
        }

        // Keep the member on screen; otherwise show the first one (in the sample data: one with a photo).
        var keep = rows.FirstOrDefault(r => r.Summary.Id == selectedId);
        if (keep is null && Context.IsSample && !sampleSelected)
        {
            sampleSelected = true;
            keep = rows.FirstOrDefault(r => r.Summary.PhotoUrl is not null && r.Summary.Membership?.Status == SubscriptionStatus.Active);
        }

        keep ??= rows.FirstOrDefault();
        MembersList.SelectedItem = keep;
        if (keep is not null)
        {
            MembersList.ScrollIntoView(keep);
        }
    }

    private void ShowCount()
    {
        var language = Context.Language;
        CountText.Text = BidiText.FixNumbers(
            total > rows.Count ? ReceptionText.MembersShown.Format(language, rows.Count, total)
                : total == 1 ? ReceptionText.MembersFoundOne.For(language) : ReceptionText.MembersFound.Format(language, total),
            language);
    }

    private async void OnMemberSelected(object sender, SelectionChangedEventArgs e)
    {
        if (MembersList.SelectedItem is MemberRow row && row.Summary.Id != member?.Id)
        {
            await LoadDetailsAsync(row.Summary.Id).ConfigureAwait(true);
        }
        else if (MembersList.SelectedItem is null && rows.Count == 0)
        {
            selectedId = null;
            member = null;
            ShowDetails();
        }
    }

    // Details

    private async Task LoadDetailsAsync(Guid id)
    {
        var mine = ++detailVersion;
        selectedId = id;
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var memberTask = Context.Api.GetMemberAsync(id);
            var paymentsTask = Context.Api.GetMemberSubscriptionsAsync(id);
            var visitsTask = Context.Api.GetCheckInsAsync(today.AddDays(-90), today, id);
            var loaded = await memberTask.ConfigureAwait(true);
            var loadedPayments = await paymentsTask.ConfigureAwait(true);
            var loadedVisits = await visitsTask.ConfigureAwait(true);
            var photo = await PhotoCache.GetAsync(Context.Api, loaded.PhotoUrl).ConfigureAwait(true);
            if (mine != detailVersion)
            {
                return;
            }

            member = loaded;
            payments = loadedPayments;
            visits = loadedVisits.OrderByDescending(c => c.At).Take(30).ToList();
            memberPhoto = photo;
            ShowDetails();
        }
        catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException or TaskCanceledException)
        {
            ReceptionErrors.Show(Context, ex);
        }
    }

    private void ShowDetails()
    {
        if (member is not { } m)
        {
            DetailsPanel.Visibility = Visibility.Collapsed;
            NoMember.Visibility = Visibility.Visible;
            return;
        }

        var language = Context.Language;
        DetailsPanel.Visibility = Visibility.Visible;
        NoMember.Visibility = Visibility.Collapsed;
        DetailPhoto.Source = memberPhoto;
        BidiText.SetText(DetailName, m.FullName);
        DetailIdLine.Text = Parts.IdLine(m.MemberNoText, m.Phone, language);
        DetailPill.Status = m.Membership?.Status;
        DetailStatus.Text = BidiText.FixNumbers(ReceptionDialogs.CurrentLine(m.Membership, language), language);
        TypePill.Visibility = m.Type == MemberType.PrivateTrainer ? Visibility.Visible : Visibility.Collapsed;
        TypePill.Text = ReceptionText.TypeTrainer.For(language);
        RenewButton.Content = (m.Membership is null ? ReceptionText.FirstPayment : ReceptionText.Renew).For(language);

        FactsGrid.Children.Clear();
        AddFact(ReceptionText.FatherName, m.FatherName);
        AddFact(ReceptionText.MotherName, m.MotherName);
        AddFact(ReceptionText.BirthDate, m.BirthDate is { } birth ? DateText.ShortDate(birth, language) : null);
        AddFact(ReceptionText.BirthPlace, m.BirthPlace);
        AddFact(ReceptionText.NationalNo, m.NationalNo, leftToRight: true);
        AddFact(ReceptionText.MobileNumber, ReceptionPhone.Format(m.Phone), leftToRight: true);
        AddFact(ReceptionText.MemberSince, DateText.ShortDate(DateOnly.FromDateTime(m.CreatedAt.ToLocalTime()), language));
        if (m.Type == MemberType.PrivateTrainer)
        {
            AddFact(ReceptionText.PrivateTrainingFact, ReceptionDialogs.ServiceLine(m.PrivateService, language));
        }
        else
        {
            AddFact(ReceptionText.TrainerFact, m.PrivateTrainerName ?? ReceptionText.NoTrainer.For(language));
        }

        PaymentsGrid.ItemsSource = payments
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PaymentRow(
                (p.Kind == SubscriptionKind.Membership ? ReceptionText.KindMembership : ReceptionText.KindPrivate).For(language),
                BidiText.FixNumbers(ReceptionFormat.Months(p.Months, language), language),
                BidiText.FixNumbers(ReceptionText.PeriodFrom.Format(language, DateText.ShortDate(p.StartDate, language)), language),
                BidiText.FixNumbers(ReceptionText.PeriodTo.Format(language, DateText.ShortDate(p.EndDate, language)), language),
                p.PricePaid))
            .ToList();
        CheckInsGrid.ItemsSource = visits
            .Select(c => new VisitRow(
                BidiText.FixNumbers(DateText.ShortDate(DateOnly.FromDateTime(c.At.ToLocalTime()), language), language),
                BidiText.FixNumbers($"{DateText.Time(c.At)} · {c.Station}", language),
                CheckInCard.ShortLabel(c.Result).For(language),
                CheckInCard.ToneOf(c.Result) switch
                {
                    CardTone.Good => PillTone.Good,
                    CardTone.Warning => PillTone.Warning,
                    _ => PillTone.Bad,
                }))
            .ToList();
    }

    private void AddFact(LocalText caption, string? value, bool leftToRight = false)
    {
        var fact = Parts.Fact(caption.For(Context.Language), "", out var valueText);
        fact.Margin = new Thickness(0, 0, 12, 10);
        if (string.IsNullOrWhiteSpace(value))
        {
            valueText.Text = "—";
        }
        else if (leftToRight)
        {
            valueText.Text = value;
            valueText.FlowDirection = FlowDirection.LeftToRight;
            valueText.HorizontalAlignment = HorizontalAlignment.Left;
        }
        else
        {
            valueText.HorizontalAlignment = HorizontalAlignment.Left;
            BidiText.SetText(valueText, BidiText.FixNumbers(value, Context.Language));
        }

        FactsGrid.Children.Add(fact);
    }

    // Actions

    private async void OnRenewClick(object sender, RoutedEventArgs e) => await RunAsync(async m =>
    {
        if (await ReceptionDialogs.RenewAsync(Context, m, memberPhoto).ConfigureAwait(true) is not null)
        {
            await RefreshAsync().ConfigureAwait(true);
        }
    }).ConfigureAwait(true);

    private async void OnQrClick(object sender, RoutedEventArgs e) => await RunAsync(async m =>
    {
        if (await ReceptionDialogs.ShowQrAsync(Context, m).ConfigureAwait(true))
        {
            await RefreshAsync().ConfigureAwait(true);
        }
    }).ConfigureAwait(true);

    private async void OnPhoneClick(object sender, RoutedEventArgs e) => await RunAsync(async m =>
    {
        if (await ReceptionDialogs.EditPhoneAsync(Context, m).ConfigureAwait(true) is not null)
        {
            await RefreshAsync().ConfigureAwait(true);
        }
    }).ConfigureAwait(true);

    private async void OnPhotoClick(object sender, RoutedEventArgs e) => await RunAsync(async m =>
    {
        if (await ReceptionDialogs.ChangePhotoAsync(Context, m).ConfigureAwait(true))
        {
            await RefreshAsync().ConfigureAwait(true);
        }
    }).ConfigureAwait(true);

    private async Task RunAsync(Func<MemberDto, Task> action)
    {
        if (member is not { } m)
        {
            return;
        }

        try
        {
            await action(m).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException or TaskCanceledException)
        {
            ReceptionErrors.Show(Context, ex);
        }
    }

    private async Task RefreshAsync()
    {
        await SearchAsync(CancellationToken.None).ConfigureAwait(true);
        if (selectedId is { } id)
        {
            await LoadDetailsAsync(id).ConfigureAwait(true);
        }
    }

    private void OnLiveChange()
    {
        if (IsVisible)
        {
            liveRefresh.Stop();
            liveRefresh.Start();
        }
    }

    // QR: the webcam, or a USB scanner typing the code (anywhere on the page)

    private void OnScanToggled(object sender, RoutedEventArgs e)
    {
        var on = ScanToggle.IsChecked == true;
        ScanToggle.SetResourceReference(BackgroundProperty, on ? "Brush.AccentSoft" : "Brush.Panel");
        ScanPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on)
        {
            StartScan();
        }
        else
        {
            StopScan();
        }
    }

    private void StartScan()
    {
        if (Context.IsSample)
        {
            ScanCamera.ShowSample();
            return;
        }

        if (!scanning)
        {
            CameraFeed.Shared.Acquire(this, Context.Station.CameraName, decodeQr: true);
            CameraFeed.Shared.QrCodeFound += OnQrCodeFound;
            ScanCamera.Attach(CameraFeed.Shared);
            scanning = true;
        }
    }

    private void StopScan()
    {
        if (scanning)
        {
            CameraFeed.Shared.QrCodeFound -= OnQrCodeFound;
            ScanCamera.Detach();
            CameraFeed.Shared.Release(this);
            scanning = false;
        }
    }

    private async void OnQrCodeFound(string code)
    {
        ScanToggle.IsChecked = false;
        Search.Text = MemberQrCode.Normalize(code);
        await SearchAsync(CancellationToken.None).ConfigureAwait(true);
    }

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

        ScanToggle.IsChecked = false;
        StopScan();
        wedge.Reset();
    }

    private async void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsVisible || e.IsRepeat || (Keyboard.FocusedElement is DependencyObject focused && focused is TextBoxBase && !IsAncestorOf(focused)))
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Enter)
        {
            if (wedge.Enter(e.Timestamp) is { } scanned)
            {
                e.Handled = true;
                Search.Text = MemberQrCode.Normalize(scanned);
                await SearchAsync(CancellationToken.None).ConfigureAwait(true);
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

    /// <summary>A search result.</summary>
    public sealed class MemberRow : ObservableObject
    {
        private ImageSource? photo;
        private string idLine;

        public MemberRow(MemberSummary summary, AppLanguage language)
        {
            Summary = summary;
            idLine = Parts.IdLine(summary.MemberNoText, summary.Phone, language);
        }

        public MemberSummary Summary { get; }

        public string Name => Summary.FullName;

        public FlowDirection NameDirection => BidiText.HasRtl(Name) ? UiLanguage.Instance.FlowDirection : FlowDirection.LeftToRight;

        public string IdLine
        {
            get => idLine;
            private set => SetProperty(ref idLine, value);
        }

        public SubscriptionStatus? Status => Summary.Membership?.Status;

        public ImageSource? Photo
        {
            get => photo;
            private set => SetProperty(ref photo, value);
        }

        public void ApplyLanguage(AppLanguage language)
        {
            IdLine = Parts.IdLine(Summary.MemberNoText, Summary.Phone, language);
            OnPropertyChanged(nameof(NameDirection));
        }

        public async Task LoadPhotoAsync(IGymApi api) => Photo = await PhotoCache.GetAsync(api, Summary.PhotoUrl).ConfigureAwait(true);
    }

    /// <summary>A payment in the details.</summary>
    public sealed record PaymentRow(string What, string Months, string From, string To, long Paid);

    /// <summary>A check-in in the details.</summary>
    public sealed record VisitRow(string Day, string Detail, string Result, PillTone Tone);
}
