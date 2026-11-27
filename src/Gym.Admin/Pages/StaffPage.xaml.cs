using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Gym.Admin.Controls;
using Gym.Admin.Logic;
using Gym.Client;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;
using Gym.UI.Controls;
using Gym.UI.Shell;

namespace Gym.Admin.Pages;

public static class StaffText
{
    public static LocalText ListTitle { get; } = new("Staff", "الموظفون");

    public static LocalText CountText { get; } = new("Staff: {0} · Club trainers: {1}", "الموظفون: {0} · مدربو النادي: {1}");

    public static LocalText AddStaff { get; } = new("Add staff", "إضافة موظف");

    public static LocalText EditStaff { get; } = new("Edit staff member", "تعديل بيانات الموظف");

    public static LocalText ShowInactive { get; } = new("Show inactive", "إظهار غير الفعّالين");

    public static LocalText ColumnName { get; } = new("Name", "الاسم");

    public static LocalText ColumnJob { get; } = new("Job", "الوظيفة");

    public static LocalText ColumnLogin { get; } = new("Login", "حساب الدخول");

    public static LocalText ColumnPhone { get; } = new("Phone", "الهاتف");

    public static LocalText ColumnStatus { get; } = new("Status", "الحالة");

    public static LocalText TypeAdmin { get; } = new("Management", "الإدارة");

    public static LocalText TypeReception { get; } = new("Reception", "الاستقبال");

    public static LocalText TypeCashier { get; } = new("Cashier", "الكاشير");

    public static LocalText TypeClubTrainer { get; } = new("Club trainer", "مدرب النادي");

    public static LocalText NoLogin { get; } = new("No login", "بلا حساب دخول");

    public static LocalText NoPassword { get; } = new("no password yet", "بلا كلمة مرور بعد");

    public static LocalText LastLogin { get; } = new("Last sign-in", "آخر تسجيل دخول");

    public static LocalText Never { get; } = new("Never", "لم يسجّل الدخول بعد");

    public static LocalText Since { get; } = new("Added", "تاريخ الإضافة");

    public static LocalText ResetPassword { get; } = new("Reset password", "تعيين كلمة مرور");

    public static LocalText ResetTitle { get; } = new("New password for {0}", "كلمة مرور جديدة لـ {0}");

    public static LocalText NewPassword { get; } = new("New password", "كلمة المرور الجديدة");

    public static LocalText PasswordHint { get; } = new("6 to 128 characters.", "من 6 إلى 128 حرفًا.");

    public static LocalText PasswordChanged { get; } = new(
        "Password changed. This person's old sign-ins stop working.",
        "تم تغيير كلمة المرور. تتوقف جلسات الدخول القديمة لهذا الشخص.");

    public static LocalText Ratings { get; } = new("Monthly ratings", "التقييمات الشهرية");

    public static LocalText RateMonth { get; } = new("Rate a month", "تقييم شهر");

    public static LocalText NoRatings { get; } = new("No ratings yet. Rate this trainer's month.", "لا توجد تقييمات بعد. قيّم شهر هذا المدرب.");

    public static LocalText OnlyTrainers { get; } = new("Monthly ratings are for club trainers.", "التقييم الشهري لمدربي النادي فقط.");

    public static LocalText AverageText { get; } = new("Average {0} of 5 · {1}", "المتوسط {0} من 5 · {1}");

    public static LocalText RatedBy { get; } = new("{1} · rated by {0}", "{1} · قيّمه {0}");

    public static LocalText SelectSomeone { get; } = new("Choose a staff member", "اختر موظفًا");

    public static LocalText SelectSomeoneText { get; } = new("Their details and ratings show here.", "تظهر هنا بياناته وتقييماته.");

    public static LocalText PhoneLabel { get; } = new("Phone", "الهاتف");

    public static LocalText JobLabel { get; } = new("Job", "الوظيفة");

    public static LocalText ActiveLabel { get; } = new("Active", "فعّال");

    public static LocalText CanLogIn { get; } = new("Can sign in to the TITANFIT apps", "يمكنه الدخول إلى برامج TITANFIT");

    public static LocalText RoleLabel { get; } = new("Role", "الصلاحية");

    public static LocalText RoleHint { get; } = new(
        "Admin: everything. Reception: members and check-in. Cashier: cafeteria.",
        "المسؤول: كل شيء. الاستقبال: المشتركون والدخول. الكاشير: الكافتيريا.");

    public static LocalText SignOutNote { get; } = new(
        "Changing the login, role, password or active state signs this person out on every PC.",
        "تغيير حساب الدخول أو الصلاحية أو كلمة المرور أو حالة التفعيل يُخرج هذا الشخص من كل الأجهزة.");

    public static LocalText RateTitle { get; } = new("Rate {0}", "تقييم {0}");

    public static LocalText MonthLabel { get; } = new("Month", "الشهر");

    public static LocalText StarsLabel { get; } = new("Stars", "النجوم");

    public static LocalText StarsValue { get; } = new("{0} of 5 stars", "{0} من 5 نجوم");

    public static LocalText ReplaceHint { get; } = new(
        "This month already has a rating; saving replaces it.",
        "لهذا الشهر تقييم سابق؛ الحفظ يستبدله.");

    public static LocalText RatingSaved { get; } = new("Rating saved.", "تم حفظ التقييم.");
}

/// <summary>Staff and ratings: the staff list, the selected person's details and login, and club trainer ratings.</summary>
public partial class StaffPage : PageView
{
    private IReadOnlyList<StaffDto> staff = [];
    private IReadOnlyList<RatingDto> ratings = [];
    private int? selectedId;
    private bool firstLoad = true;
    private bool showing;
    private int detailVersion;

    public StaffPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    private StaffDto? Selected => staff.FirstOrDefault(s => s.Id == selectedId);

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        staff = await Context.Api.GetStaffAsync(includeInactive: true, ct: cancellationToken).ConfigureAwait(true);
        if (firstLoad)
        {
            firstLoad = false;
            selectedId ??= (staff.FirstOrDefault(s => s.Type == StaffType.ClubTrainer && s.IsActive) ?? staff.FirstOrDefault())?.Id;
        }

        ShowList();
        await LoadRatingsAsync(cancellationToken).ConfigureAwait(true);
    }

    public override void OnLanguageChanged()
    {
        ShowList();
        ShowDetail();
    }

    /// <summary>"Management", "Reception", "Cashier", "Club trainer".</summary>
    public static LocalText JobName(StaffType type) => type switch
    {
        StaffType.Admin => StaffText.TypeAdmin,
        StaffType.Reception => StaffText.TypeReception,
        StaffType.Cashier => StaffText.TypeCashier,
        _ => StaffText.TypeClubTrainer,
    };

    private void ShowList()
    {
        var language = Context.Language;
        var showInactive = InactiveBox.IsChecked == true;
        CountText.Text = StaffText.CountText.Format(
            language,
            staff.Count(s => s.IsActive),
            staff.Count(s => s.IsActive && s.Type == StaffType.ClubTrainer));
        var rows = staff
            .Where(s => showInactive || s.IsActive || s.Id == selectedId)
            .OrderBy(s => s.IsActive ? 0 : 1).ThenBy(s => s.Type).ThenBy(s => s.FullName, StringComparer.CurrentCulture)
            .Select(s => ToRow(s, language))
            .ToList();
        showing = true;
        StaffGrid.ItemsSource = rows;
        StaffGrid.SelectedItem = rows.FirstOrDefault(r => r.Staff.Id == selectedId);
        showing = false;
        if (StaffGrid.SelectedItem is StaffRow row)
        {
            StaffGrid.ScrollIntoView(row);
        }
    }

    private static StaffRow ToRow(StaffDto s, AppLanguage language)
    {
        var hasLogin = s.Username is not null;
        return new StaffRow(
            s,
            s.FullName,
            JobName(s.Type).For(language),
            hasLogin ? s.Username! : StaffText.NoLogin.For(language),
            hasLogin ? FlowDirection.LeftToRight : language.ToFlowDirection(),
            hasLogin ? AppRoles.RoleName(s.Role).For(language) : "",
            hasLogin ? Visibility.Visible : Visibility.Collapsed,
            PhoneText.Format(s.Phone),
            (s.IsActive ? AdminText.Active : AdminText.Inactive).For(language),
            s.IsActive ? PillTone.Good : PillTone.Neutral);
    }

    private async Task LoadRatingsAsync(CancellationToken cancellationToken)
    {
        var mine = ++detailVersion;
        var person = Selected;
        IReadOnlyList<RatingDto> loaded = person is { Type: StaffType.ClubTrainer }
            ? await Context.Api.GetRatingsAsync(person.Id, cancellationToken).ConfigureAwait(true)
            : [];
        if (mine != detailVersion)
        {
            return;
        }

        ratings = loaded;
        ShowDetail();
    }

    private void ShowDetail()
    {
        var person = Selected;
        NoSelection.Visibility = person is null ? Visibility.Visible : Visibility.Collapsed;
        DetailPanel.Visibility = person is null ? Visibility.Collapsed : Visibility.Visible;
        if (person is null)
        {
            return;
        }

        var language = Context.Language;
        BidiText.SetText(DetailName, person.FullName);
        DetailIcon.Data = person.Type == StaffType.ClubTrainer ? UiIcons.Dumbbell : UiIcons.User;
        DetailJob.Text = JobName(person.Type).For(language);
        DetailStatus.Tone = person.IsActive ? PillTone.Good : PillTone.Neutral;
        DetailStatus.Text = (person.IsActive ? AdminText.Active : AdminText.Inactive).For(language);
        DetailPhone.Text = PhoneText.Format(person.Phone);
        if (person.Username is { } username)
        {
            DetailLogin.Text = username;
            DetailLogin.FlowDirection = FlowDirection.LeftToRight;
            var role = AppRoles.RoleName(person.Role).For(language);
            DetailRole.Text = person.CanLogIn || !person.IsActive ? role : role + " · " + StaffText.NoPassword.For(language);
            DetailRole.Visibility = Visibility.Visible;
            ResetButton.Visibility = Visibility.Visible;
        }
        else
        {
            DetailLogin.Text = StaffText.NoLogin.For(language);
            DetailLogin.ClearValue(FlowDirectionProperty);
            DetailRole.Visibility = Visibility.Collapsed;
            ResetButton.Visibility = Visibility.Collapsed;
        }

        DetailLastLogin.Text = person.LastLoginAt is { } at ? DateText.DateAndTime(at, language) : StaffText.Never.For(language);
        DetailSince.Text = DateText.ShortDate(DateOnly.FromDateTime(person.CreatedAt.ToLocalTime()), language);

        var trainer = person.Type == StaffType.ClubTrainer;
        RatingsSection.Visibility = trainer ? Visibility.Visible : Visibility.Collapsed;
        NotTrainerText.Visibility = trainer ? Visibility.Collapsed : Visibility.Visible;
        if (!trainer)
        {
            RatingsList.ItemsSource = null;
            return;
        }

        AverageText.Text = RatingMonths.Average(ratings) is { } average
            ? StaffText.AverageText.Format(language, average.ToString("0.0", CultureInfo.InvariantCulture), AdminText.Months(ratings.Count, language))
            : "";
        RatingsList.ItemsSource = ratings.Select(r => new RatingRow(
            DateText.Month(r.Year, r.Month, language),
            Enumerable.Range(1, StaffRating.MaxStars).Select(i => i <= r.Stars).ToList(),
            r.Note ?? "",
            string.IsNullOrWhiteSpace(r.Note) ? Visibility.Collapsed : Visibility.Visible,
            StaffText.RatedBy.Format(language, r.RatedBy, DateText.ShortDate(DateOnly.FromDateTime(r.UpdatedAt.ToLocalTime()), language))))
            .ToList();
        RatingsEmpty.Visibility = ratings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task ReloadAsync()
    {
        try
        {
            await OnShownAsync(CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Any failure is shown as a toast; the page stays usable.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.ShowError(ex);
        }
    }

    private static List<Choice> JobChoices(AppLanguage language) =>
        [.. new[] { StaffType.Reception, StaffType.Cashier, StaffType.ClubTrainer, StaffType.Admin }.Select(t => new Choice(t, JobName(t).For(language)))];

    private static List<Choice> RoleChoices(AppLanguage language) =>
        [.. new[] { StaffRole.Reception, StaffRole.Cashier, StaffRole.Admin }.Select(r => new Choice(r, AppRoles.RoleName(r).For(language)))];

    private static void SelectRole(ComboBox box, StaffRole? role)
    {
        box.SelectedItem = role is { } value ? box.Items.OfType<Choice>().FirstOrDefault(c => Equals(c.Value, value)) : null;
    }

    private static LocalText Named(LocalText text, string name) =>
        new(text.Format(AppLanguage.English, name), text.Format(AppLanguage.Arabic, name));

    private async Task EditAsync(StaffDto? person)
    {
        var language = Context.Language;
        var creating = person is null;
        var hadLogin = person?.Username is not null;
        var needsPassword = !hadLogin; // a new login gets its password here; an existing one uses "Reset password"

        var name = Forms.Text(CommonText.FullName, person?.FullName, maxLength: StaffMember.NameMaxLength);
        var phone = Forms.Text(StaffText.PhoneLabel, person?.Phone, leftToRight: true, maxLength: Member.PhoneMaxLength);
        var type = Forms.Pick(StaffText.JobLabel, JobChoices(language), person?.Type ?? StaffType.Reception);
        var active = Forms.Check(StaffText.ActiveLabel, person?.IsActive ?? true);
        active.VerticalAlignment = VerticalAlignment.Bottom;
        active.Margin = new Thickness(0, 0, 0, 12);
        var canLogIn = Forms.Check(StaffText.CanLogIn, hadLogin);
        var username = Forms.Text(CommonText.Username, person?.Username, leftToRight: true, maxLength: StaffMember.UsernameMaxLength);
        var role = Forms.Pick(StaffText.RoleLabel, RoleChoices(language), null, StaffText.RoleHint);
        SelectRole(role.Box, person?.Role ?? StaffMember.DefaultRoleFor(person?.Type ?? StaffType.Reception));
        var password = Forms.Password(CommonText.Password, StaffText.PasswordHint);
        var confirm = Forms.Password(CommonText.ConfirmPassword);

        var loginRows = new List<UIElement> { Forms.Columns(username.Field, role.Field) };
        if (needsPassword)
        {
            loginRows.Add(Forms.Columns(password.Field, confirm.Field));
        }

        var loginPanel = Forms.Stack([.. loginRows]);
        loginPanel.Visibility = hadLogin ? Visibility.Visible : Visibility.Collapsed;
        canLogIn.Checked += (_, _) => loginPanel.Visibility = Visibility.Visible;
        canLogIn.Unchecked += (_, _) => loginPanel.Visibility = Visibility.Collapsed;

        // Until the admin picks a role himself, it follows the job (a club trainer has no default role).
        var roleChosen = person?.Role is not null;
        var settingRole = false;
        role.Box.SelectionChanged += (_, _) => roleChosen |= !settingRole;
        type.Box.SelectionChanged += (_, _) =>
        {
            if (roleChosen || type.Box.SelectedItem is not Choice { Value: StaffType job })
            {
                return;
            }

            settingRole = true;
            SelectRole(role.Box, StaffMember.DefaultRoleFor(job));
            settingRole = false;
        };

        var error = Forms.Note("");
        error.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Bad");
        error.Visibility = Visibility.Collapsed;

        var parts = new List<UIElement>
        {
            Forms.Columns(name.Field, phone.Field),
            creating ? Forms.Columns(type.Field, new Border()) : Forms.Columns(type.Field, active),
            canLogIn,
            loginPanel,
        };
        if (!creating)
        {
            parts.Add(Forms.Note(StaffText.SignOutNote.For(language)));
        }

        parts.Add(error);

        await Context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = creating ? StaffText.AddStaff : StaffText.EditStaff,
            Content = Forms.Stack([.. parts]),
            PrimaryText = CommonText.Save,
            Width = 640,
            OnPrimary = async () =>
            {
                error.Visibility = Visibility.Collapsed;
                var (fullName, nameError) = FormChecks.Required(name.Box.Text);
                name.Field.Error = nameError?.For(language);
                var job = type.Box.SelectedItem is Choice { Value: StaffType t } ? t : StaffType.Reception;
                var login = canLogIn.IsChecked == true;
                string? user = null;
                StaffRole? chosenRole = null;
                string? newPassword = null;
                var ok = nameError is null;
                username.Field.Error = null;
                role.Field.Error = null;
                password.Field.Error = null;
                confirm.Field.Error = null;
                if (login)
                {
                    var (userValue, userError) = FormChecks.Required(username.Box.Text);
                    username.Field.Error = userError?.For(language);
                    user = userValue;
                    chosenRole = role.Box.SelectedItem is Choice { Value: StaffRole r } ? r : null;
                    role.Field.Error = chosenRole is null ? AdminErrors.RoleRequired.For(language) : null;
                    ok &= userError is null && chosenRole is not null;
                    if (needsPassword)
                    {
                        newPassword = password.Box.Password;
                        if (newPassword.Length is < StaffMember.PasswordMinLength or > StaffMember.PasswordMaxLength)
                        {
                            password.Field.Error = AdminErrors.InvalidPassword.For(language);
                            ok = false;
                        }
                        else if (newPassword != confirm.Box.Password)
                        {
                            confirm.Field.Error = CommonText.PasswordsDiffer.For(language);
                            ok = false;
                        }
                    }
                }

                if (!ok || fullName is null)
                {
                    return false;
                }

                StaffDto saved;
                try
                {
                    if (person is null)
                    {
                        saved = await Context.Api.CreateStaffAsync(new CreateStaffRequest(
                            fullName, job, FormChecks.Optional(phone.Box.Text), user, newPassword, chosenRole)).ConfigureAwait(true);
                    }
                    else
                    {
                        saved = await Context.Api.UpdateStaffAsync(person.Id, new UpdateStaffRequest(
                            fullName, job, active.IsChecked == true, FormChecks.Optional(phone.Box.Text), user, chosenRole)).ConfigureAwait(true);
                        if (login && newPassword is not null)
                        {
                            await Context.Api.ResetStaffPasswordAsync(person.Id, newPassword).ConfigureAwait(true);
                        }
                    }
                }
                catch (GymApiException ex)
                {
                    var message = AdminErrors.Describe(ex, language);
                    switch (ex.Code)
                    {
                        case "invalid_username" or "duplicate_username" or "username_required":
                            username.Field.Error = message;
                            break;
                        case "invalid_password":
                            password.Field.Error = message;
                            break;
                        case "role_required":
                            role.Field.Error = message;
                            break;
                        default:
                            error.Text = message;
                            error.Visibility = Visibility.Visible;
                            break;
                    }

                    return false;
                }

                Context.Ui.Toast(AdminText.Saved);
                selectedId = saved.Id;
                await OnShownAsync(CancellationToken.None).ConfigureAwait(true);
                return true;
            },
        }).ConfigureAwait(true);
    }

    /// <summary>Smoke screenshots: the edit dialog of the chosen person (not awaited: the dialog stays open).</summary>
    internal void EditSelectedForSmoke()
    {
        if (Selected is { } person)
        {
            _ = EditAsync(person);
        }
    }

    /// <summary>Smoke screenshots: the rating dialog of the chosen club trainer.</summary>
    internal void RateSelectedForSmoke()
    {
        if (Selected is { Type: StaffType.ClubTrainer } person)
        {
            _ = RateAsync(person);
        }
    }

    private async Task ResetPasswordAsync(StaffDto person)
    {
        var language = Context.Language;
        var password = Forms.Password(StaffText.NewPassword, StaffText.PasswordHint);
        var confirm = Forms.Password(CommonText.ConfirmPassword);
        await Context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = Named(StaffText.ResetTitle, person.FullName),
            Content = Forms.Stack(password.Field, confirm.Field, Forms.Note(StaffText.SignOutNote.For(language))),
            PrimaryText = CommonText.Save,
            OnPrimary = async () =>
            {
                var value = password.Box.Password;
                password.Field.Error = value.Length is < StaffMember.PasswordMinLength or > StaffMember.PasswordMaxLength
                    ? AdminErrors.InvalidPassword.For(language)
                    : null;
                confirm.Field.Error = password.Field.Error is null && value != confirm.Box.Password ? CommonText.PasswordsDiffer.For(language) : null;
                if (password.Field.Error is not null || confirm.Field.Error is not null)
                {
                    return false;
                }

                try
                {
                    await Context.Api.ResetStaffPasswordAsync(person.Id, value).ConfigureAwait(true);
                }
                catch (GymApiException ex)
                {
                    password.Field.Error = AdminErrors.Describe(ex, language);
                    return false;
                }

                Context.Ui.Toast(StaffText.PasswordChanged);
                await ReloadAsync().ConfigureAwait(true);
                return true;
            },
        }).ConfigureAwait(true);
    }

    private async Task RateAsync(StaffDto trainer)
    {
        var language = Context.Language;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var history = ratings;
        var month = Forms.Pick(
            StaffText.MonthLabel,
            RatingMonths.Recent(today).Select(m => new Choice(m, DateText.Month(m.Year, m.Month, language))),
            RatingMonths.Default(today));

        var stars = 0;
        var buttons = new List<Button>();
        var starsRow = new StackPanel { Orientation = Orientation.Horizontal };
        var starsText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        starsText.SetResourceReference(StyleProperty, "Text.Caption");
        void Paint()
        {
            foreach (var button in buttons)
            {
                var on = (int)button.Tag <= stars;
                button.SetResourceReference(ForegroundProperty, on ? "Brush.Gold" : "Brush.TextSecondary");
                button.SetResourceReference(BorderBrushProperty, on ? "Brush.GoldLine" : "Brush.Line");
            }

            starsText.Text = stars == 0 ? "" : StaffText.StarsValue.Format(language, stars);
        }

        for (var i = StaffRating.MinStars; i <= StaffRating.MaxStars; i++)
        {
            var value = i;
            var button = new Button { Tag = value, Margin = new Thickness(value == StaffRating.MinStars ? 0 : 6, 0, 0, 0) };
            button.SetResourceReference(StyleProperty, "Button.Icon");
            ThemeAssist.SetIcon(button, UiIcons.Star);
            AutomationProperties.SetName(button, StaffText.StarsValue.Format(language, value));
            button.Click += (_, _) =>
            {
                stars = value;
                Paint();
            };
            buttons.Add(button);
            starsRow.Children.Add(button);
        }

        starsRow.Children.Add(starsText);
        var starsField = Forms.Field(StaffText.StarsLabel, starsRow, null);
        var note = Forms.MultiLine(AdminText.Note, null, StaffRating.NoteMaxLength);
        var replaceHint = Forms.Note(StaffText.ReplaceHint.For(language));
        replaceHint.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Warning");

        RatingMonth Chosen() => month.Box.SelectedItem is Choice { Value: RatingMonth m } ? m : RatingMonths.Default(today);
        void Prefill()
        {
            var existing = RatingMonths.Find(history, Chosen());
            stars = existing?.Stars ?? 0;
            note.Box.Text = existing?.Note ?? "";
            replaceHint.Visibility = existing is null ? Visibility.Collapsed : Visibility.Visible;
            starsField.Error = null;
            Paint();
        }

        month.Box.SelectionChanged += (_, _) => Prefill();
        Prefill();

        await Context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = Named(StaffText.RateTitle, trainer.FullName),
            Content = Forms.Stack(month.Field, replaceHint, starsField, note.Field),
            PrimaryText = CommonText.Save,
            Tone = DialogTone.Gold,
            OnPrimary = async () =>
            {
                if (stars is < StaffRating.MinStars or > StaffRating.MaxStars)
                {
                    starsField.Error = AdminErrors.InvalidStars.For(language);
                    return false;
                }

                var chosen = Chosen();
                try
                {
                    await Context.Api.SetRatingAsync(trainer.Id, chosen.Year, chosen.Month, new SetRatingRequest(stars, FormChecks.Optional(note.Box.Text)))
                        .ConfigureAwait(true);
                }
                catch (GymApiException ex)
                {
                    starsField.Error = AdminErrors.Describe(ex, language);
                    return false;
                }

                Context.Ui.Toast(StaffText.RatingSaved);
                await ReloadAsync().ConfigureAwait(true);
                return true;
            },
        }).ConfigureAwait(true);
    }

    private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (showing)
        {
            return;
        }

        selectedId = (StaffGrid.SelectedItem as StaffRow)?.Staff.Id;
        try
        {
            ShowDetail();
            await LoadRatingsAsync(CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Any failure is shown as a toast; the page stays usable.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.ShowError(ex);
        }
    }

    private void OnInactiveChanged(object sender, RoutedEventArgs e) => ShowList();

    private async void OnAddClick(object sender, RoutedEventArgs e) => await EditAsync(null).ConfigureAwait(true);

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (Selected is { } person)
        {
            await EditAsync(person).ConfigureAwait(true);
        }
    }

    private async void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (StaffGrid.SelectedItem is StaffRow row)
        {
            await EditAsync(row.Staff).ConfigureAwait(true);
        }
    }

    private async void OnResetClick(object sender, RoutedEventArgs e)
    {
        if (Selected is { Username: not null } person)
        {
            await ResetPasswordAsync(person).ConfigureAwait(true);
        }
    }

    private async void OnRateClick(object sender, RoutedEventArgs e)
    {
        if (Selected is { Type: StaffType.ClubTrainer } trainer)
        {
            await RateAsync(trainer).ConfigureAwait(true);
        }
    }

    /// <summary>A row of the staff list.</summary>
    public sealed record StaffRow(
        StaffDto Staff,
        string Name,
        string Job,
        string Login,
        FlowDirection LoginFlow,
        string Role,
        Visibility RoleVisibility,
        string Phone,
        string StatusText,
        PillTone Tone);

    /// <summary>A month in the ratings history: 5 stars (true = given).</summary>
    public sealed record RatingRow(string MonthText, IReadOnlyList<bool> Stars, string Note, Visibility NoteVisibility, string ByText);
}
