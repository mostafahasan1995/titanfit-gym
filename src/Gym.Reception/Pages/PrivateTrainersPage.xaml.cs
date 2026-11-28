using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Gym.Client;
using Gym.Contracts;
using Gym.Domain;
using Gym.Reception.Logic;
using Gym.Reception.Views;
using Gym.UI;
using Gym.UI.Controls;
using Gym.UI.Shell;

namespace Gym.Reception.Pages;

/// <summary>
/// Private trainers (docs/PLAN.md, "Private trainer"): their private training service, their linked clients, and
/// turning a member into a private trainer or back (the server refuses while clients are linked).
/// </summary>
public partial class PrivateTrainersPage : PageView
{
    private readonly DispatcherTimer liveRefresh;
    private readonly List<TrainerRow> trainers = [];
    private readonly List<ClientRow> clients = [];
    private Guid? selectedId;
    private ImageSource? trainerPhoto;
    private decimal monthlyFee;
    private int detailVersion;

    public PrivateTrainersPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        liveRefresh = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        liveRefresh.Tick += async (_, _) =>
        {
            liveRefresh.Stop();
            await ReloadAsync(CancellationToken.None).ConfigureAwait(true);
        };
        Context.Live.MemberChanged += change => Dispatcher.InvokeAsync(() =>
        {
            if (IsVisible)
            {
                liveRefresh.Stop();
                liveRefresh.Start();
            }
        });
        Context.Live.SettingsChanged += settings => Dispatcher.InvokeAsync(() =>
        {
            monthlyFee = settings.PrivateTrainingMonthlyFee;
            ShowDetails();
        });
        System.ComponentModel.PropertyChangedEventManager.AddHandler(ExchangeRates.Current, OnRateChanged, string.Empty);
        ShowDetails();
    }

    private TrainerRow? Selected => trainers.FirstOrDefault(t => t.Trainer.Id == selectedId);

    public override Task OnShownAsync(CancellationToken cancellationToken) => ReloadAsync(cancellationToken);

    public override void OnLanguageChanged()
    {
        foreach (var row in trainers)
        {
            row.ApplyLanguage(Context.Language);
        }

        foreach (var row in clients)
        {
            row.ApplyLanguage(Context.Language);
        }

        ShowDetails();
    }

    /// <summary>Smoke screenshots: the pay dialog for the selected trainer (paid in <paramref name="currency"/>).</summary>
    internal Task ShowPaySampleAsync(Currency currency = Currency.Usd)
    {
        if (Selected is { } row)
        {
            _ = PayAsync(row.Trainer, currency);
        }

        return Task.CompletedTask;
    }

    private async Task ReloadAsync(CancellationToken ct)
    {
        try
        {
            var list = await Context.Api.GetPrivateTrainersAsync(ct).ConfigureAwait(true);
            monthlyFee = (await Context.Api.GetSettingsAsync(ct).ConfigureAwait(true)).PrivateTrainingMonthlyFee;
            trainers.Clear();
            trainers.AddRange(list.Select(t => new TrainerRow(t, Context.Language)));
            TrainersList.ItemsSource = null;
            TrainersList.ItemsSource = trainers;
            NoTrainers.Visibility = trainers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            TrainerCount.Text = trainers.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            foreach (var row in trainers)
            {
                _ = row.LoadPhotoAsync(Context.Api);
            }

            var keep = Selected ?? trainers.FirstOrDefault();
            selectedId = keep?.Trainer.Id;
            TrainersList.SelectedItem = keep;
            await LoadClientsAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException)
        {
            ReceptionErrors.Show(Context, ex);
        }
    }

    private async void OnTrainerSelected(object sender, SelectionChangedEventArgs e)
    {
        if (TrainersList.SelectedItem is TrainerRow row && row.Trainer.Id != selectedId)
        {
            selectedId = row.Trainer.Id;
            ShowDetails();
            await LoadClientsAsync().ConfigureAwait(true);
        }
    }

    private async Task LoadClientsAsync()
    {
        var mine = ++detailVersion;
        clients.Clear();
        trainerPhoto = null;
        if (Selected is not { } row)
        {
            ShowDetails();
            return;
        }

        try
        {
            var list = await Context.Api.GetPrivateTrainerClientsAsync(row.Trainer.Id).ConfigureAwait(true);
            var photo = await PhotoCache.GetAsync(Context.Api, row.Trainer.PhotoUrl).ConfigureAwait(true);
            if (mine != detailVersion)
            {
                return;
            }

            trainerPhoto = photo;
            clients.AddRange(list.Select(c => new ClientRow(c, Context.Language)));
            foreach (var client in clients)
            {
                _ = client.LoadPhotoAsync(Context.Api);
            }
        }
        catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException)
        {
            ReceptionErrors.Show(Context, ex);
        }

        ShowDetails();
    }

    private void OnRateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => ShowDetails();

    private void ShowDetails()
    {
        if (Selected is not { } row)
        {
            DetailsPanel.Visibility = Visibility.Collapsed;
            NoTrainer.Visibility = trainers.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            return;
        }

        var language = Context.Language;
        var trainer = row.Trainer;
        DetailsPanel.Visibility = Visibility.Visible;
        NoTrainer.Visibility = Visibility.Collapsed;
        TrainerPhoto.Source = trainerPhoto;
        BidiText.SetText(TrainerName, trainer.FullName);
        TrainerIdLine.Text = Parts.IdLine(trainer.MemberNoText, trainer.Phone, language);
        ServicePill.Tone = row.ServiceTone;
        ServicePill.Text = BidiText.FixNumbers(ReceptionDialogs.ServiceLine(trainer.PrivateService, language), language);
        MembershipPill.Status = trainer.Membership?.Status;
        MembershipText.Text = BidiText.FixNumbers(ReceptionDialogs.CurrentLine(trainer.Membership, language), language);
        FeeText.Text = BidiText.FixNumbers(
            ReceptionText.MonthlyFee.Format(language, Money.FormatWithSyp(monthlyFee, ExchangeRates.Current.ToSyp(monthlyFee), language)), language);
        PayButton.Content = (trainer.PrivateService is null ? ReceptionText.PayPrivate : ReceptionText.RenewPrivate).For(language);

        ClientsList.ItemsSource = null;
        ClientsList.ItemsSource = clients;
        NoClients.Visibility = clients.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClientCount.Text = BidiText.FixNumbers(ReceptionText.ClientCount.Format(language, clients.Count), language);
    }

    // Private training service

    private async void OnPayClick(object sender, RoutedEventArgs e)
    {
        if (Selected is { } row)
        {
            await PayAsync(row.Trainer).ConfigureAwait(true);
        }
    }

    private async Task PayAsync(PrivateTrainerDto trainer, Currency currency = Currency.Usd)
    {
        try
        {
            var paid = await ReceptionDialogs.PayPrivateServiceAsync(
                Context, trainer.Id, trainer.FullName, trainer.MemberNoText, trainer.Phone, trainer.PrivateService, trainerPhoto, currency).ConfigureAwait(true);
            if (paid is not null)
            {
                await ReloadAsync(CancellationToken.None).ConfigureAwait(true);
            }
        }
        catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException or TaskCanceledException)
        {
            ReceptionErrors.Show(Context, ex);
        }
    }

    // Clients

    private async void OnLinkClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row)
        {
            return;
        }

        var trainer = row.Trainer;
        var names = trainers.ToDictionary(t => t.Trainer.Id, t => t.Trainer.FullName);
        var linked = await ReceptionDialogs.PickMemberAsync(
            Context,
            ReceptionText.LinkClientTitle,
            ReceptionText.LinkClient,
            member => member.Id != trainer.Id && member.PrivateTrainerId != trainer.Id,
            member => member.PrivateTrainerId is { } other && other != trainer.Id
                ? ReceptionText.TrainsWith.Format(Context.Language, names.GetValueOrDefault(other) ?? "—")
                : member.PrivateTrainerId == trainer.Id ? ReceptionText.AlreadyClient.For(Context.Language) : null,
            member => Context.Api.LinkClientAsync(trainer.Id, member.Id)).ConfigureAwait(true);
        if (linked is not null)
        {
            Context.Ui.Toast(ReceptionText.ClientLinked);
            await ReloadAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnUnlinkClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row || sender is not FrameworkElement { Tag: ClientRow client })
        {
            return;
        }

        var sure = await Context.Ui.ConfirmAsync(ReceptionText.UnlinkTitle, ReceptionText.UnlinkText, ReceptionText.Unlink, danger: true).ConfigureAwait(true);
        if (!sure)
        {
            return;
        }

        try
        {
            await Context.Api.UnlinkClientAsync(row.Trainer.Id, client.Summary.Id).ConfigureAwait(true);
            Context.Ui.Toast(ReceptionText.ClientUnlinked);
            await ReloadAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException or TaskCanceledException)
        {
            ReceptionErrors.Show(Context, ex);
        }
    }

    // Member type

    private async void OnAddTrainerClick(object sender, RoutedEventArgs e)
    {
        var added = await ReceptionDialogs.PickMemberAsync(
            Context,
            ReceptionText.AddTrainerTitle,
            ReceptionText.MakeTrainer,
            member => member.Type == MemberType.Regular,
            member => member.Type == MemberType.PrivateTrainer ? ReceptionText.AlreadyTrainer.For(Context.Language) : null,
            async member =>
            {
                var full = await Context.Api.GetMemberAsync(member.Id).ConfigureAwait(true);
                await Context.Api.UpdateMemberAsync(member.Id, ReceptionDialogs.ToUpdate(full) with { Type = MemberType.PrivateTrainer }).ConfigureAwait(true);
            }).ConfigureAwait(true);
        if (added is not null)
        {
            selectedId = added.Id;
            Context.Ui.Toast(ReceptionText.TrainerAdded);
            await ReloadAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnMakeRegularClick(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row)
        {
            return;
        }

        var sure = await Context.Ui.ConfirmAsync(ReceptionText.MakeRegularTitle, ReceptionText.MakeRegularText, ReceptionText.MakeRegular).ConfigureAwait(true);
        if (!sure)
        {
            return;
        }

        try
        {
            var full = await Context.Api.GetMemberAsync(row.Trainer.Id).ConfigureAwait(true);
            await Context.Api.UpdateMemberAsync(full.Id, ReceptionDialogs.ToUpdate(full) with { Type = MemberType.Regular }).ConfigureAwait(true);
            selectedId = null;
            Context.Ui.Toast(ReceptionText.TrainerRemoved);
            await ReloadAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (GymApiException ex) when (ex.Code == "trainer_has_clients")
        {
            await Context.Ui.ShowMessageAsync(ReceptionText.UnlinkFirstTitle, ReceptionText.ErrorTrainerHasClients).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException or TaskCanceledException)
        {
            ReceptionErrors.Show(Context, ex);
        }
    }

    /// <summary>A private trainer in the list.</summary>
    public sealed class TrainerRow : ObservableObject
    {
        private ImageSource? photo;
        private string idLine = "";
        private string serviceText = "";

        public TrainerRow(PrivateTrainerDto trainer, AppLanguage language)
        {
            Trainer = trainer;
            ServiceTone = trainer.PrivateService?.Status switch
            {
                SubscriptionStatus.Active => PillTone.Good,
                SubscriptionStatus.ExpiringSoon => PillTone.Warning,
                SubscriptionStatus.Expired => PillTone.Bad,
                _ => PillTone.Neutral,
            };
            ApplyLanguage(language);
        }

        public PrivateTrainerDto Trainer { get; }

        public string Name => Trainer.FullName;

        public FlowDirection NameDirection => BidiText.HasRtl(Name) ? UiLanguage.Instance.FlowDirection : FlowDirection.LeftToRight;

        public string IdLine
        {
            get => idLine;
            private set => SetProperty(ref idLine, value);
        }

        public PillTone ServiceTone { get; }

        public string ServiceText
        {
            get => serviceText;
            private set => SetProperty(ref serviceText, value);
        }

        public ImageSource? Photo
        {
            get => photo;
            private set => SetProperty(ref photo, value);
        }

        public void ApplyLanguage(AppLanguage language)
        {
            IdLine = BidiText.FixNumbers(ReceptionText.TrainerLine.Format(language, Trainer.MemberNoText, ReceptionText.ClientCount.Format(language, Trainer.ClientCount)), language);
            ServiceText = BidiText.FixNumbers(Trainer.PrivateService switch
            {
                null => ReceptionText.ServiceShortNone.For(language),
                { Status: SubscriptionStatus.Expired } ended => ReceptionText.ServiceShortEnded.Format(language, DateText.ShortDate(ended.EndDate, language)),
                { } paid => ReceptionText.ServiceShortPaid.Format(language, DateText.ShortDate(paid.EndDate, language)),
            }, language);
            OnPropertyChanged(nameof(NameDirection));
        }

        public async Task LoadPhotoAsync(IGymApi api) => Photo = await PhotoCache.GetAsync(api, Trainer.PhotoUrl).ConfigureAwait(true);
    }

    /// <summary>A client of the selected trainer.</summary>
    public sealed class ClientRow : ObservableObject
    {
        private ImageSource? photo;
        private string idLine = "";

        public ClientRow(MemberSummary summary, AppLanguage language)
        {
            Summary = summary;
            ApplyLanguage(language);
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
}
