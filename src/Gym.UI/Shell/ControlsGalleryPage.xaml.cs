using System.Windows.Media;
using Gym.Domain;
using Gym.UI.Controls;

namespace Gym.UI.Shell;

/// <summary>Strings of the control gallery.</summary>
public static class GalleryText
{
    public static LocalText Title { get; } = new("Controls", "عناصر الواجهة");

    public static LocalText Buttons { get; } = new("Buttons", "الأزرار");

    public static LocalText Inputs { get; } = new("Inputs", "حقول الإدخال");

    public static LocalText SearchAndChoices { get; } = new("Search and choices", "البحث والخيارات");

    public static LocalText StatusAndMoney { get; } = new("Status and money", "الحالة والمبالغ");

    public static LocalText Messages { get; } = new("Messages", "الرسائل");

    public static LocalText Members { get; } = new("Members", "المشتركون");

    public static LocalText Member { get; } = new("Member", "المشترك");

    public static LocalText Status { get; } = new("Status", "الحالة");

    public static LocalText Ends { get; } = new("Valid until", "صالح حتى");

    public static LocalText Renew { get; } = new("Renew", "تجديد");

    public static LocalText Disabled { get; } = new("Disabled", "غير متاح");

    public static LocalText NamePlaceholder { get; } = new("e.g. Omar Al-Khatib", "مثال: عمر الخطيب");

    public static LocalText Plan { get; } = new("Plan", "الاشتراك");

    public static LocalText StartDate { get; } = new("Start date", "تاريخ البدء");

    public static LocalText Active { get; } = new("Active", "فعّال");

    public static LocalText PrivateTrainer { get; } = new("Private trainer", "مدرب خاص");

    public static LocalText Phone { get; } = new("Phone", "الهاتف");

    public static LocalText PhoneTooShort { get; } = new("Enter the full number, e.g. 0944 123 456.", "أدخل الرقم كاملًا، مثل 0944 123 456.");

    public static LocalText Months { get; } = new("{0} months · {1}", "{0} أشهر · {1}");

    public static LocalText OneMonth { get; } = new("1 month · {0}", "شهر واحد · {0}");
}

/// <summary>Every shared control with sample data (see the XAML).</summary>
public partial class ControlsGalleryPage : PageView
{
    private IReadOnlyList<Contracts.PlanDto> plans = [];
    private IReadOnlyList<(Contracts.MemberSummary Member, ImageSource? Photo)> members = [];

    public ControlsGalleryPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        NoStatusPill.Status = null;
        DateBox.SelectedDate = DateTime.Today;
        OnLanguageChanged();
    }

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        plans = await Context.Api.GetPlansAsync(ct: cancellationToken).ConfigureAwait(true);
        var page = await Context.Api.SearchMembersAsync(pageSize: 3, ct: cancellationToken).ConfigureAwait(true);
        var list = new List<(Contracts.MemberSummary, ImageSource?)>();
        foreach (var member in page.Items)
        {
            var photo = member.PhotoUrl is { } url ? Images.FromBytes(await Context.Api.GetBytesAsync(url, cancellationToken).ConfigureAwait(true), 96) : null;
            list.Add((member, photo));
        }

        members = list;
        OnLanguageChanged();
    }

    public override void OnLanguageChanged()
    {
        var language = UiLanguage.Current;
        PhoneField.Error = BidiText.FixNumbers(GalleryText.PhoneTooShort.For(language), language);
        var selected = PlanBox.SelectedIndex;
        PlanBox.ItemsSource = plans.Select(p => p.Months == 1
            ? GalleryText.OneMonth.Format(language, Money.Format(p.Price, language))
            : GalleryText.Months.Format(language, p.Months, Money.Format(p.Price, language))).ToList();
        PlanBox.SelectedIndex = selected >= 0 ? selected : (plans.Count > 1 ? 1 : -1);
        MembersGrid.ItemsSource = members.Select(m => new GalleryRow(
            m.Member.FullName,
            m.Member.MemberNoText,
            m.Member.Membership?.Status,
            m.Member.Membership is { } s ? DateText.ShortDate(s.EndDate, language) : "—",
            m.Photo)).ToList();
    }

    /// <summary>A row of the members grid.</summary>
    public sealed record GalleryRow(string Name, string Number, SubscriptionStatus? Status, string Ends, ImageSource? Photo);
}
