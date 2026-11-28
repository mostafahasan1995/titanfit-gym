using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Gym.Admin.Controls;
using Gym.Admin.Logic;
using Gym.Client;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;
using Gym.UI.Controls;
using Gym.UI.Shell;

namespace Gym.Admin.Pages;

public static class MembersText
{
    public static LocalText All { get; } = new("All", "الكل");

    public static LocalText AllTypes { get; } = new("All members", "كل المشتركين");

    public static LocalText Regular { get; } = new("Regular members", "مشتركون عاديون");

    public static LocalText PrivateTrainers { get; } = new("Private trainers", "المدربون الخاصون");

    public static LocalText PrivateTrainer { get; } = new("Private trainer", "مدرب خاص");

    public static LocalText RegularMember { get; } = new("Regular member", "مشترك عادي");

    public static LocalText Member { get; } = new("Member", "المشترك");

    public static LocalText Phone { get; } = new("Phone", "الهاتف");

    public static LocalText Status { get; } = new("Status", "الحالة");

    public static LocalText ValidUntil { get; } = new("Valid until", "صالح حتى");

    public static LocalText PageOf { get; } = new("{0}–{1} of {2}", "{0}-{1} من أصل {2}");

    public static LocalText Previous { get; } = new("Previous page", "الصفحة السابقة");

    public static LocalText Next { get; } = new("Next page", "الصفحة التالية");

    public static LocalText PickMember { get; } = new("Choose a member", "اختر مشتركًا");

    public static LocalText PickMemberText { get; } = new("Their status, payments and check-ins show here.", "تظهر هنا حالته ودفعاته ومرات دخوله.");

    public static LocalText Details { get; } = new("Details", "البيانات");

    public static LocalText NationalNo { get; } = new("National no.", "الرقم الوطني");

    public static LocalText Born { get; } = new("Born", "الولادة");

    public static LocalText Mother { get; } = new("Mother", "اسم الأم");

    public static LocalText Registered { get; } = new("Registered", "تاريخ التسجيل");

    public static LocalText Notes { get; } = new("Notes", "ملاحظات");

    public static LocalText QrCode { get; } = new("QR code", "رمز QR");

    public static LocalText Trainer { get; } = new("Private trainer", "المدرب الخاص");

    public static LocalText ActiveUntil { get; } = new("Valid until {0} · {1} left", "صالح حتى {0} · المتبقي {1}");

    public static LocalText ExpiresToday { get; } = new("Valid until {0} · last day today", "صالح حتى {0} · اليوم آخر يوم");

    public static LocalText ExpiredAgo { get; } = new("Ended on {0} · {1} ago", "انتهى في {0} · منذ {1}");

    public static LocalText NeverPaid { get; } = new("Registered, never paid. Renew at reception.", "مسجّل ولم يدفع بعد. يتم التجديد من الاستقبال.");

    public static LocalText ClientsTitle { get; } = new("Private trainer · {0}", "مدرب خاص · {0}");

    public static LocalText ClientsNone { get; } = new("no clients", "بلا متدربين");

    public static LocalText ClientsCount { get; } = new("{0} clients", "عدد المتدربين {0}");

    public static LocalText ServiceUntil { get; } = new("Training service valid until {0}", "خدمة التدريب صالحة حتى {0}");

    public static LocalText ServiceEnded { get; } = new("Training service ended on {0}", "انتهت خدمة التدريب في {0}");

    public static LocalText ServiceNone { get; } = new("No private training service paid yet.", "لم تُدفع خدمة التدريب الخاص بعد.");

    public static LocalText Payments { get; } = new("Payments", "الدفعات");

    public static LocalText NoPayments { get; } = new("No payments yet.", "لا توجد دفعات بعد.");

    public static LocalText MembershipOf { get; } = new("Membership · {0}", "اشتراك · {0}");

    public static LocalText PrivateServiceOf { get; } = new("Private training · {0}", "تدريب خاص · {0}");

    public static LocalText PeriodText { get; } = new("{0} to {1}", "من {0} إلى {1}");

    public static LocalText PaidBy { get; } = new("Paid {0} · {1}", "دُفع {0} · {1}");

    public static LocalText CheckInsTitle { get; } = new("Check-ins · last 30 days ({0})", "الدخول · آخر 30 يومًا ({0})");

    public static LocalText NoCheckIns { get; } = new("No check-ins in the last 30 days.", "لا يوجد دخول في آخر 30 يومًا.");

    public static LocalText Admitted { get; } = new("Admitted", "دخل");

    public static LocalText Refused { get; } = new("Refused", "مرفوض");

    public static LocalText EditMember { get; } = new("Edit member", "تعديل بيانات المشترك");

    public static LocalText FirstName { get; } = new("First name", "الاسم");

    public static LocalText FatherName { get; } = new("Father's name", "اسم الأب");

    public static LocalText LastName { get; } = new("Surname", "النسبة");

    public static LocalText MotherName { get; } = new("Mother's name", "اسم الأم");

    public static LocalText BirthDate { get; } = new("Birth date", "تاريخ الولادة");

    public static LocalText BirthPlace { get; } = new("Birth place", "مكان الولادة");

    public static LocalText MemberType { get; } = new("Member type", "نوع المشترك");

    public static LocalText EditNote { get; } = new(
        "Renewals, photos, QR codes and trainer links are done at reception.",
        "التجديد والصور ورموز QR وربط المتدربين تتم من برنامج الاستقبال.");
}

/// <summary>Members: search, filters, details, payments and check-ins; edit the member's data.</summary>
public partial class MembersPage : PageView
{
    private const int PageSize = 20;

    private readonly Dictionary<Guid, ImageSource?> photos = [];
    private PagedResult<MemberSummary>? result;
    private int page = 1;
    private Guid? selectedId;
    private MemberDetail? detail;
    private bool ready;

    public MembersPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        FillTypeBox();
        ready = true;
    }

    public override async Task OnShownAsync(CancellationToken cancellationToken) => await LoadListAsync(cancellationToken).ConfigureAwait(true);

    public override void OnLanguageChanged()
    {
        ready = false;
        FillTypeBox();
        ready = true;
        ShowList();
        ShowDetail();
    }

    private void FillTypeBox()
    {
        var selected = Forms.Selected<MemberType?>(TypeBox);
        var language = Context.Language;
        var choices = new List<Choice>
        {
            new(null, MembersText.AllTypes.For(language)),
            new(MemberType.Regular, MembersText.Regular.For(language)),
            new(MemberType.PrivateTrainer, MembersText.PrivateTrainers.For(language)),
        };
        TypeBox.ItemsSource = choices;
        TypeBox.SelectedItem = choices.FirstOrDefault(c => Equals(c.Value, selected)) ?? choices[0];
    }

    private SubscriptionStatus? StatusFilter =>
        ActiveButton.IsChecked == true ? SubscriptionStatus.Active
        : ExpiringButton.IsChecked == true ? SubscriptionStatus.ExpiringSoon
        : ExpiredButton.IsChecked == true ? SubscriptionStatus.Expired
        : null;

    private async Task LoadListAsync(CancellationToken ct)
    {
        var query = SearchBox.Text.Trim();
        result = await Context.Api.SearchMembersAsync(
            query.Length == 0 ? null : query, StatusFilter, TypeBox.SelectedItem is Choice { Value: MemberType type } ? type : null, page, PageSize, ct)
            .ConfigureAwait(true);
        await LoadPhotosAsync(result.Items, ct).ConfigureAwait(true);

        // Keep the chosen member when he is still in the list, else the first one.
        var keep = result.Items.FirstOrDefault(m => m.Id == selectedId) ?? result.Items.FirstOrDefault();
        selectedId = keep?.Id;
        ShowList();
        if (keep is null)
        {
            detail = null;
            ShowDetail();
        }
        else if (keep.Id != detail?.Member.Id)
        {
            await LoadDetailAsync(keep.Id, ct).ConfigureAwait(true);
        }
    }

    private async Task LoadPhotosAsync(IEnumerable<MemberSummary> members, CancellationToken ct)
    {
        var missing = members.Where(m => m.PhotoUrl is not null && !photos.ContainsKey(m.Id)).ToList();
        var loads = missing.Select(async m =>
        {
            try
            {
                return (m.Id, Image: (ImageSource?)Images.FromBytes(await Context.Api.GetBytesAsync(m.PhotoUrl!, ct).ConfigureAwait(true), 96));
            }
            catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException)
            {
                return (m.Id, Image: (ImageSource?)null); // the list still shows, with the person icon
            }
        });
        foreach (var (id, image) in await Task.WhenAll(loads).ConfigureAwait(true))
        {
            photos[id] = image;
        }
    }

    private void ShowList()
    {
        if (result is not { } r)
        {
            return;
        }

        var language = Context.Language;

        // English status labels are longer ("No subscription"), Arabic dates are longer ("28 تشرين الثاني 2026").
        // Shares of the table width (with a minimum each), so the table also fits a narrow window.
        StatusColumn.Width = new DataGridLength(language == AppLanguage.Arabic ? 1.4 : 1.78, DataGridLengthUnitType.Star);
        EndsColumn.Width = new DataGridLength(language == AppLanguage.Arabic ? 1.72 : 1.28, DataGridLengthUnitType.Star);
        var rows = r.Items.Select(m => new MemberRow(
            m,
            m.FullName,
            DirectionOf(m.FullName),
            m.MemberNoText,
            m.Type == MemberType.PrivateTrainer ? MembersText.PrivateTrainer.For(language) : "",
            PhoneText.Format(m.Phone),
            m.Membership?.Status,
            m.Membership is { } s ? DateText.ShortDate(s.EndDate, language) : AdminText.Dash.For(language),
            photos.GetValueOrDefault(m.Id))).ToList();
        var selected = selectedId;
        MembersGrid.ItemsSource = rows;
        MembersGrid.SelectedItem = rows.FirstOrDefault(x => x.Member.Id == selected);
        ListEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var first = r.Total == 0 ? 0 : ((r.Page - 1) * r.PageSize) + 1;
        var last = Math.Min(r.Total, r.Page * r.PageSize);
        PageText.Text = BidiText.FixNumbers(MembersText.PageOf.Format(language, NumberText.Format(first), NumberText.Format(last), NumberText.Format(r.Total)), language);
        PreviousButton.IsEnabled = r.Page > 1;
        NextButton.IsEnabled = last < r.Total;
    }

    private async Task LoadDetailAsync(Guid id, CancellationToken ct)
    {
        selectedId = id;
        var member = await Context.Api.GetMemberAsync(id, ct).ConfigureAwait(true);
        var payments = await Context.Api.GetMemberSubscriptionsAsync(id, ct).ConfigureAwait(true);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var checkIns = await Context.Api.GetCheckInsAsync(today.AddDays(-29), today, id, ct).ConfigureAwait(true);
        IReadOnlyList<MemberSummary> clients = member.Type == MemberType.PrivateTrainer
            ? await Context.Api.GetPrivateTrainerClientsAsync(id, ct).ConfigureAwait(true)
            : [];
        if (!photos.ContainsKey(id) && member.PhotoUrl is not null)
        {
            await LoadPhotosAsync([new MemberSummary(id, member.MemberNo, member.MemberNoText, member.Type, member.FullName, member.Phone, member.NationalNo, member.PhotoUrl, member.PrivateTrainerId, member.Membership)], ct).ConfigureAwait(true);
        }

        if (selectedId != id)
        {
            return; // another member was chosen meanwhile
        }

        detail = new MemberDetail(member, payments, checkIns, clients);
        ShowDetail();
    }

    private void ShowDetail()
    {
        var language = Context.Language;
        if (detail is not { } d)
        {
            DetailPanel.Visibility = Visibility.Collapsed;
            DetailEmpty.Visibility = Visibility.Visible;
            return;
        }

        var m = d.Member;
        DetailEmpty.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Visible;
        DetailPhoto.Source = photos.GetValueOrDefault(m.Id);
        BidiText.SetText(DetailName, m.FullName);
        DetailNumber.Text = m.MemberNoText;
        DetailType.Text = (m.Type == MemberType.PrivateTrainer ? MembersText.PrivateTrainer : MembersText.RegularMember).For(language);
        DetailStatus.Status = m.Membership?.Status;
        DetailStatusText.Text = StatusLine(m.Membership, language);

        var facts = new List<Fact>
        {
            Fact.Ltr(MembersText.Phone.For(language), PhoneText.Format(m.Phone)),
            Fact.Ltr(MembersText.NationalNo.For(language), m.NationalNo ?? AdminText.Dash.For(language)),
        };
        if (m.BirthDate is { } born)
        {
            facts.Add(Fact.Data(MembersText.Born.For(language), DateText.ShortDate(born, language)));
        }

        if (!string.IsNullOrWhiteSpace(m.BirthPlace))
        {
            facts.Add(Fact.Data(MembersText.BirthPlace.For(language), m.BirthPlace));
        }

        if (!string.IsNullOrWhiteSpace(m.MotherName))
        {
            facts.Add(Fact.Data(MembersText.Mother.For(language), m.MotherName));
        }

        if (m.PrivateTrainerName is { Length: > 0 } trainer)
        {
            facts.Add(Fact.Data(MembersText.Trainer.For(language), trainer));
        }

        facts.Add(Fact.Data(MembersText.Registered.For(language), DateText.ShortDate(DateOnly.FromDateTime(m.CreatedAt.ToLocalTime()), language)));
        if (!string.IsNullOrWhiteSpace(m.Notes))
        {
            facts.Add(Fact.Data(MembersText.Notes.For(language), m.Notes));
        }

        DetailFacts.ItemsSource = facts;

        TrainerSection.Visibility = m.Type == MemberType.PrivateTrainer ? Visibility.Visible : Visibility.Collapsed;
        if (m.Type == MemberType.PrivateTrainer)
        {
            TrainerTitle.Text = MembersText.ClientsTitle.Format(
                language, d.Clients.Count == 0 ? MembersText.ClientsNone.For(language) : MembersText.ClientsCount.Format(language, NumberText.Format(d.Clients.Count)));
            TrainerService.Text = m.PrivateService switch
            {
                null => MembersText.ServiceNone.For(language),
                { Status: SubscriptionStatus.Expired } s => MembersText.ServiceEnded.Format(language, DateText.ShortDate(s.EndDate, language)),
                { } s => MembersText.ServiceUntil.Format(language, DateText.ShortDate(s.EndDate, language)),
            };
            ClientList.ItemsSource = d.Clients.Select(c => new ClientRow(c.FullName, DirectionOf(c.FullName), c.Membership?.Status)).ToList();
        }

        PaymentList.ItemsSource = d.Payments.OrderByDescending(p => p.CreatedAt).Select(p => new PaymentRow(
            (p.Kind == SubscriptionKind.PrivateTraining ? MembersText.PrivateServiceOf : MembersText.MembershipOf).Format(language, AdminText.Months(p.Months, language)),
            MembersText.PeriodText.Format(language, DateText.ShortDate(p.StartDate, language), DateText.ShortDate(p.EndDate, language)),
            MembersText.PaidBy.Format(language, DateText.DateAndTime(p.CreatedAt, language), p.CreatedBy),
            p.PricePaid,
            CurrencyLine(p, language),
            p.Currency == Currency.Syp ? Visibility.Visible : Visibility.Collapsed)).ToList();
        NoPayments.Visibility = d.Payments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        CheckInsTitle.Text = MembersText.CheckInsTitle.Format(language, NumberText.Format(d.CheckIns.Count(c => c.Admitted)));
        CheckInList.ItemsSource = d.CheckIns.OrderByDescending(c => c.At).Take(12).Select(c => new CheckInRow(
            DateText.DateAndTime(c.At, language),
            (c.Admitted ? MembersText.Admitted : MembersText.Refused).For(language),
            c.Admitted ? PillTone.Good : PillTone.Bad)).ToList();
        NoCheckIns.Visibility = d.CheckIns.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>A payment in pounds: "Paid 230,000 SYP at $1 = 11,500 SYP". Empty for a payment in dollars.</summary>
    private static string CurrencyLine(SubscriptionDto payment, AppLanguage language) =>
        payment.Currency == Currency.Syp && payment.ExchangeRate is { } rate
            ? BidiText.FixNumbers(
                CommonText.PaidInSypAtRate.Format(language, Money.FormatSyp(payment.PaidAmount, language), Money.FormatSyp(rate, language)), language)
            : "";

    private static string StatusLine(SubscriptionStatusDto? status, AppLanguage language) => status switch
    {
        null => MembersText.NeverPaid.For(language),
        { Status: SubscriptionStatus.Expired } s => MembersText.ExpiredAgo.Format(language, DateText.ShortDate(s.EndDate, language), AdminText.Days(s.DaysOverdue, language)),
        { DaysLeft: 0 } s => MembersText.ExpiresToday.Format(language, DateText.ShortDate(s.EndDate, language)),
        { } s => MembersText.ActiveUntil.Format(language, DateText.ShortDate(s.EndDate, language), AdminText.Days(s.DaysLeft, language)),
    };

    /// <summary>Smoke screenshots: the edit dialog of the chosen member (not awaited: the dialog stays open).</summary>
    internal void EditForSmoke() => _ = EditAsync();

    private async Task EditAsync()
    {
        if (detail is not { } d)
        {
            return;
        }

        var m = d.Member;
        var language = Context.Language;
        var first = Forms.Text(MembersText.FirstName, m.FirstName, maxLength: 60);
        var father = Forms.Text(MembersText.FatherName, m.FatherName, maxLength: 60);
        var last = Forms.Text(MembersText.LastName, m.LastName, maxLength: 60);
        var mother = Forms.Text(MembersText.MotherName, m.MotherName, maxLength: 60);
        var birthDate = Forms.Date(MembersText.BirthDate, m.BirthDate);
        var birthPlace = Forms.Text(MembersText.BirthPlace, m.BirthPlace, maxLength: 60);
        var nationalNo = Forms.Text(MembersText.NationalNo, m.NationalNo, leftToRight: true, maxLength: 20);
        var phone = Forms.Text(MembersText.Phone, m.Phone, leftToRight: true, maxLength: 20);
        var type = Forms.Pick(
            MembersText.MemberType,
            [new Choice(MemberType.Regular, MembersText.RegularMember.For(language)), new Choice(MemberType.PrivateTrainer, MembersText.PrivateTrainer.For(language))],
            m.Type);
        var notes = Forms.MultiLine(MembersText.Notes, m.Notes, maxLength: 500);
        var content = Forms.Stack(
            Forms.Columns(first.Field, father.Field),
            Forms.Columns(last.Field, mother.Field),
            Forms.Columns(birthDate.Field, birthPlace.Field),
            Forms.Columns(nationalNo.Field, phone.Field),
            type.Field,
            notes.Field,
            Forms.Note(MembersText.EditNote.For(language)));

        await Context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = MembersText.EditMember,
            Content = content,
            PrimaryText = CommonText.Save,
            Width = 640,
            OnPrimary = async () =>
            {
                var (firstName, firstError) = FormChecks.Required(first.Box.Text);
                var (lastName, lastError) = FormChecks.Required(last.Box.Text);
                first.Field.Error = firstError?.For(language);
                last.Field.Error = lastError?.For(language);
                if (firstName is null || lastName is null)
                {
                    return false;
                }

                var request = new UpdateMemberRequest(
                    firstName,
                    lastName,
                    FormChecks.Optional(father.Box.Text),
                    FormChecks.Optional(mother.Box.Text),
                    birthDate.Box.SelectedDate is { } date ? DateOnly.FromDateTime(date) : null,
                    FormChecks.Optional(birthPlace.Box.Text),
                    FormChecks.Optional(nationalNo.Box.Text),
                    FormChecks.Optional(phone.Box.Text),
                    FormChecks.Optional(notes.Box.Text),
                    Forms.Selected<MemberType>(type.Box));
                try
                {
                    await Context.Api.UpdateMemberAsync(m.Id, request).ConfigureAwait(true);
                }
                catch (GymApiException ex)
                {
                    Context.Ui.Toast(AdminErrors.Describe(ex, language), ToastKind.Error);
                    return false;
                }

                Context.Ui.Toast(AdminText.Saved);
                detail = null;
                await LoadListAsync(CancellationToken.None).ConfigureAwait(true);
                return true;
            },
        }).ConfigureAwait(true);
    }

    private static FlowDirection DirectionOf(string? text) => BidiText.HasRtl(text) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    private async Task RunAsync(Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Show any failure as a toast; the page stays usable.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.ShowError(ex);
        }
    }

    private async void OnSearchChanged(object? sender, EventArgs e)
    {
        if (!ready)
        {
            return;
        }

        page = 1;
        await RunAsync(() => LoadListAsync(CancellationToken.None)).ConfigureAwait(true);
    }

    private async void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        if (!ready || !IsLoaded)
        {
            return;
        }

        page = 1;
        await RunAsync(() => LoadListAsync(CancellationToken.None)).ConfigureAwait(true);
    }

    private async void OnPreviousClick(object sender, RoutedEventArgs e)
    {
        page = Math.Max(1, page - 1);
        await RunAsync(() => LoadListAsync(CancellationToken.None)).ConfigureAwait(true);
    }

    private async void OnNextClick(object sender, RoutedEventArgs e)
    {
        page++;
        await RunAsync(() => LoadListAsync(CancellationToken.None)).ConfigureAwait(true);
    }

    private async void OnMemberSelected(object sender, SelectionChangedEventArgs e)
    {
        if (MembersGrid.SelectedItem is MemberRow row && row.Member.Id != selectedId)
        {
            await RunAsync(() => LoadDetailAsync(row.Member.Id, CancellationToken.None)).ConfigureAwait(true);
        }
    }

    private async void OnEditClick(object sender, RoutedEventArgs e) => await RunAsync(EditAsync).ConfigureAwait(true);

    /// <summary>A row of the members list.</summary>
    public sealed record MemberRow(
        MemberSummary Member, string Name, FlowDirection NameDirection, string Number, string TypeText, string Phone, SubscriptionStatus? Status, string Ends, ImageSource? Photo);

    public sealed record Fact(string Label, string Value, FlowDirection Direction)
    {
        public static Fact Ltr(string label, string value) => new(label, value, FlowDirection.LeftToRight);

        public static Fact Data(string label, string value) => new(label, value, DirectionOf(value));
    }

    public sealed record ClientRow(string Name, FlowDirection NameDirection, SubscriptionStatus? Status);

    public sealed record PaymentRow(string Title, string Period, string Paid, decimal Price, string CurrencyLine, Visibility CurrencyVisibility);

    public sealed record CheckInRow(string When, string Result, PillTone Tone);

    private sealed record MemberDetail(MemberDto Member, IReadOnlyList<SubscriptionDto> Payments, IReadOnlyList<CheckInResultDto> CheckIns, IReadOnlyList<MemberSummary> Clients);
}
