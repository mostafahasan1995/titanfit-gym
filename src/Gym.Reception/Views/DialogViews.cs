using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Gym.Client;
using Gym.Contracts;
using Gym.Reception.Hardware;
using Gym.Reception.Logic;
using Gym.UI;
using Gym.UI.Controls;

namespace Gym.Reception.Views;

/// <summary>Small building blocks for the code-built dialog contents.</summary>
internal static class Parts
{
    public static TextBlock Text(string text, string style = "Text.Body", double? size = null)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.None };
        block.SetResourceReference(FrameworkElement.StyleProperty, style);
        if (size is { } s)
        {
            block.FontSize = s;
        }

        return block;
    }

    public static TextBlock Data(string? text, string style = "Text.Strong")
    {
        var block = new TextBlock { HorizontalAlignment = HorizontalAlignment.Left };
        block.SetResourceReference(FrameworkElement.StyleProperty, style);
        BidiText.SetText(block, text);
        return block;
    }

    public static StackPanel Fact(string caption, string value, out TextBlock valueText)
    {
        var panel = new StackPanel();
        var captionText = new TextBlock { Text = caption };
        captionText.SetResourceReference(FrameworkElement.StyleProperty, "Fact.Caption");
        valueText = new TextBlock { Text = value };
        valueText.SetResourceReference(FrameworkElement.StyleProperty, "Fact.Value");
        panel.Children.Add(captionText);
        panel.Children.Add(valueText);
        return panel;
    }

    /// <summary>A person line: photo, name, a caption ("#00231 · 0944 123 456") and an optional second caption.</summary>
    public static Grid Person(string name, string caption, ImageSource? photo, string? second = null)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var avatar = new Avatar { Width = 56, Height = 56, Source = photo };
        grid.Children.Add(avatar);
        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(lines, 2);
        var nameText = Data(name, "Text.Strong");
        nameText.FontSize = 17;
        lines.Children.Add(nameText);
        var captionText = new TextBlock { Text = caption, Margin = new Thickness(0, 2, 0, 0) };
        captionText.SetResourceReference(FrameworkElement.StyleProperty, "Text.Caption");
        lines.Children.Add(captionText);
        if (second is { Length: > 0 })
        {
            var secondText = new TextBlock { Text = second, Margin = new Thickness(0, 2, 0, 0) };
            secondText.SetResourceReference(FrameworkElement.StyleProperty, "Text.Caption");
            lines.Children.Add(secondText);
        }

        grid.Children.Add(lines);
        return grid;
    }

    /// <summary>"#00231 · 0944 123 456" with the numbers kept in order in Arabic.</summary>
    public static string IdLine(string memberNoText, string? phone, AppLanguage language)
    {
        var text = string.IsNullOrWhiteSpace(phone) ? memberNoText : $"{memberNoText} · {ReceptionPhone.Format(phone)}";
        return BidiText.FixNumbers(text, language);
    }
}

/// <summary>
/// The body of the renew and private-training dialogs: who, the choices (months and price), the dates the new period
/// would get (the server's renewal rule) and the total in gold.
/// </summary>
internal sealed class PeriodChoiceView : UserControl
{
    private readonly IReadOnlyList<PeriodChoice> choices;
    private readonly DateOnly? currentEnd;
    private readonly DateOnly today;
    private readonly TextBlock startText;
    private readonly TextBlock startNote;
    private readonly TextBlock endText;
    private readonly TextBlock errorText;
    private readonly MoneyText total;

    public PeriodChoiceView(FrameworkElement person, LocalText choiceLabel, IReadOnlyList<PeriodChoice> choices, DateOnly? currentEnd, DateOnly today, int? preselectMonths)
    {
        ArgumentNullException.ThrowIfNull(choices);
        this.choices = choices;
        this.currentEnd = currentEnd;
        this.today = today;
        var language = UiLanguage.Current;

        var root = new StackPanel();
        root.Children.Add(person);

        var label = Parts.Text(choiceLabel.For(language), "Text.Label");
        label.Margin = new Thickness(2, 20, 2, 8);
        root.Children.Add(label);

        var grid = new UniformGrid { Columns = Math.Min(4, Math.Max(1, choices.Count)) };
        var buttons = new List<RadioButton>();
        foreach (var choice in choices)
        {
            var content = new StackPanel();
            var months = new TextBlock { Text = ReceptionFormat.Months(choice.Months, language), FontWeight = FontWeights.SemiBold, FontSize = 15 };
            content.Children.Add(months);
            content.Children.Add(new MoneyText { Amount = choice.Price, FontSize = 14, Margin = new Thickness(0, 4, 0, 0) });
            var button = new RadioButton { Content = content, Margin = new Thickness(0, 0, 8, 8), Tag = choice };
            button.SetResourceReference(StyleProperty, "Choice.Card");
            button.Checked += (_, _) => Select(choice);
            buttons.Add(button);
            grid.Children.Add(button);
        }

        root.Children.Add(grid);

        var facts = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        facts.ColumnDefinitions.Add(new ColumnDefinition());
        facts.ColumnDefinitions.Add(new ColumnDefinition());
        var start = Parts.Fact(ReceptionText.Starts.For(language), "—", out startText);
        startNote = Parts.Text("", "Text.Caption", 12);
        startNote.Margin = new Thickness(0, 2, 0, 0);
        start.Children.Add(startNote);
        facts.Children.Add(start);
        var end = Parts.Fact(ReceptionText.Ends.For(language), "—", out endText);
        Grid.SetColumn(end, 1);
        facts.Children.Add(end);
        root.Children.Add(facts);

        var divider = new Border();
        divider.SetResourceReference(StyleProperty, "Divider");
        divider.Margin = new Thickness(0, 16, 0, 12);
        root.Children.Add(divider);

        var totalRow = new DockPanel { LastChildFill = false };
        var totalLabel = Parts.Text(ReceptionText.Total.For(language), "Text.Label");
        totalLabel.VerticalAlignment = VerticalAlignment.Center;
        totalRow.Children.Add(totalLabel);
        total = new MoneyText { FontSize = 26, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(total, Dock.Right);
        totalRow.Children.Add(total);
        root.Children.Add(totalRow);

        errorText = new TextBlock { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };
        errorText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Bad");
        root.Children.Add(errorText);

        Content = root;
        ShowDates(null);
        if (buttons.FirstOrDefault(b => b.Tag is PeriodChoice c && c.Months == preselectMonths) is { } preselected)
        {
            preselected.IsChecked = true;
        }
    }

    public PeriodChoice? Selected { get; private set; }

    /// <summary>False (and a red line) when nothing is chosen.</summary>
    public bool Validate()
    {
        if (Selected is not null)
        {
            return true;
        }

        errorText.Text = ReceptionText.ChooseDuration.For(UiLanguage.Current);
        errorText.Visibility = Visibility.Visible;
        return false;
    }

    private void Select(PeriodChoice choice)
    {
        Selected = choice;
        errorText.Visibility = Visibility.Collapsed;
        ShowDates(choice);
    }

    private void ShowDates(PeriodChoice? choice)
    {
        var language = UiLanguage.Current;
        if (choice is null)
        {
            startText.Text = "—";
            endText.Text = "—";
            startNote.Text = "";
            total.Amount = 0;
            return;
        }

        var preview = RenewPreview.For(currentEnd, today, choice.Months);
        startText.Text = DateText.ShortDate(preview.Start, language);
        endText.Text = DateText.ShortDate(preview.End, language);
        startNote.Text = (preview.ContinuesCurrent ? ReceptionText.StartsAfterCurrent : ReceptionText.StartsToday).For(language);
        total.Amount = choice.Price;
    }
}

/// <summary>One choice of <see cref="PeriodChoiceView"/>: months, price and the plan (null for the private training service).</summary>
internal sealed record PeriodChoice(int Months, long Price, int? PlanId);

/// <summary>The phone input with +963 in front (always left-to-right) and the stored form under it.</summary>
internal sealed class PhoneInput : UserControl
{
    private readonly FormField field;

    public PhoneInput(string? phone)
    {
        var language = UiLanguage.Current;
        field = new FormField { Label = ReceptionText.MobileNumber.For(language) };
        Placeholder = ReceptionText.PhonePlaceholder;
        var row = new Grid { FlowDirection = FlowDirection.LeftToRight };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var prefix = new Border { Padding = new Thickness(14, 0, 14, 0), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), MinHeight = 48 };
        prefix.SetResourceReference(Border.BackgroundProperty, "Brush.Panel");
        prefix.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
        var code = new TextBlock { Text = ReceptionPhone.CountryCode, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold };
        code.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextTertiary");
        prefix.Child = code;
        row.Children.Add(prefix);
        Box = new TextBox { Text = ReceptionPhone.ForInput(phone), FlowDirection = FlowDirection.LeftToRight };
        ThemeAssist.SetPlaceholder(Box, ReceptionText.PhonePlaceholder.For(language));
        Grid.SetColumn(Box, 2);
        row.Children.Add(Box);
        field.Content = row;
        Box.TextChanged += (_, _) => ShowHint();
        Content = field;
        ShowHint();
    }

    public TextBox Box { get; }

    private LocalText Placeholder { get; }

    /// <summary>The stored form ("0944123456"), or null for no phone.</summary>
    public string? Phone => ReceptionPhone.Normalize(Box.Text);

    /// <summary>After the language switch.</summary>
    public void ApplyLanguage()
    {
        field.Label = ReceptionText.MobileNumber.For(UiLanguage.Current);
        ThemeAssist.SetPlaceholder(Box, Placeholder.For(UiLanguage.Current));
        ShowHint();
    }

    private void ShowHint()
    {
        var language = UiLanguage.Current;
        field.Hint = Phone is { } phone
            ? BidiText.FixNumbers(ReceptionText.PhoneSavedAs.Format(language, ReceptionPhone.Format(phone)), language)
            : ReceptionText.PhoneHelp.For(language);
    }
}

/// <summary>Search a member and pick one (link a client, add a private trainer).</summary>
internal sealed class MemberPickerView : UserControl
{
    private readonly IGymApi api;
    private readonly Func<MemberSummary, bool> canPick;
    private readonly Func<MemberSummary, string?> note;
    private readonly SearchBox search;
    private readonly ListBox list;
    private readonly TextBlock errorText;
    private int version;

    /// <param name="canPick">Members that can be chosen (the others are shown dimmed).</param>
    /// <param name="note">An extra line for a member (e.g. "Trains with Maher"), or null.</param>
    public MemberPickerView(IGymApi api, Func<MemberSummary, bool> canPick, Func<MemberSummary, string?> note)
    {
        this.api = api;
        this.canPick = canPick;
        this.note = note;
        var language = UiLanguage.Current;
        var root = new DockPanel();
        search = new SearchBox();
        ThemeAssist.SetPlaceholder(search, ReceptionText.SearchMembersPlaceholder.For(language));
        search.SearchChanged += async (_, _) => await LoadAsync().ConfigureAwait(true);
        DockPanel.SetDock(search, Dock.Top);
        root.Children.Add(search);
        errorText = new TextBlock { Visibility = Visibility.Collapsed, Margin = new Thickness(2, 8, 2, 0), TextWrapping = TextWrapping.Wrap };
        errorText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Bad");
        DockPanel.SetDock(errorText, Dock.Bottom);
        root.Children.Add(errorText);
        list = new ListBox { Height = 340, Margin = new Thickness(0, 12, 0, 0) };
        list.SelectionChanged += (_, _) => errorText.Visibility = Visibility.Collapsed;
        root.Children.Add(list);
        Content = root;
        Loaded += (_, _) => search.Focus();
    }

    public MemberSummary? Selected => (list.SelectedItem as ListBoxItem)?.Tag as MemberSummary;

    public bool Validate()
    {
        if (Selected is not null)
        {
            return true;
        }

        errorText.Text = ReceptionText.ChooseMember.For(UiLanguage.Current);
        errorText.Visibility = Visibility.Visible;
        return false;
    }

    public async Task LoadAsync()
    {
        var mine = ++version;
        var language = UiLanguage.Current;
        PagedResult<MemberSummary> page;
        try
        {
            page = await api.SearchMembersAsync(search.Text, pageSize: 30).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is GymApiException or System.Net.Http.HttpRequestException or TaskCanceledException)
        {
            errorText.Text = ReceptionErrors.Describe(ex, language);
            errorText.Visibility = Visibility.Visible;
            return;
        }

        if (mine != version)
        {
            return;
        }

        list.Items.Clear();
        foreach (var member in page.Items)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var avatar = new Avatar { Width = 40, Height = 40 };
            ThemeAssist.SetCornerRadius(avatar, new CornerRadius(12));
            row.Children.Add(avatar);
            var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lines, 2);
            lines.Children.Add(Parts.Data(member.FullName));
            var caption = new TextBlock { Text = Parts.IdLine(member.MemberNoText, member.Phone, language) };
            caption.SetResourceReference(StyleProperty, "Text.Caption");
            lines.Children.Add(caption);
            if (note(member) is { Length: > 0 } extra)
            {
                var noteText = new TextBlock { Text = extra };
                noteText.SetResourceReference(StyleProperty, "Text.Caption");
                noteText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Warning");
                lines.Children.Add(noteText);
            }

            row.Children.Add(lines);
            var pill = new StatusPill { Status = member.Membership?.Status };
            Grid.SetColumn(pill, 3);
            row.Children.Add(pill);
            var item = new ListBoxItem { Content = row, Tag = member, IsEnabled = canPick(member) };
            list.Items.Add(item);
            _ = LoadPhotoAsync(avatar, member.PhotoUrl);
        }

        if (page.Items.Count == 0)
        {
            list.Items.Add(new ListBoxItem
            {
                IsEnabled = false,
                Content = new EmptyState { Title = CommonText.NoResults.For(language), Text = CommonText.NoResultsText.For(language) },
            });
        }
    }

    private async Task LoadPhotoAsync(Avatar avatar, string? url) => avatar.Source = await PhotoCache.GetAsync(api, url).ConfigureAwait(true);
}

/// <summary>Webcam preview with Take photo / Retake; the photo is cut to a square JPEG.</summary>
internal sealed class PhotoCaptureView : UserControl
{
    private readonly bool sample;
    private readonly CameraView camera;
    private readonly Image still;
    private readonly Button takeButton;
    private readonly TextBlock errorText;
    private bool attached;

    public PhotoCaptureView(string? cameraName, bool sample)
    {
        this.sample = sample;
        CameraName = cameraName;
        var language = UiLanguage.Current;
        var root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var box = new Grid { Width = 340, Height = 340 };
        camera = new CameraView { Mode = CameraMode.Photo };
        box.Children.Add(camera);
        still = new Image { Stretch = Stretch.UniformToFill, Visibility = Visibility.Collapsed, FlowDirection = FlowDirection.LeftToRight };
        box.Children.Add(still);
        root.Children.Add(box);
        takeButton = new Button { Margin = new Thickness(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, MinWidth = 200 };
        ThemeAssist.SetIcon(takeButton, UiIcons.Camera);
        takeButton.Click += (_, _) => TakeOrRetake();
        root.Children.Add(takeButton);
        errorText = new TextBlock { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center };
        errorText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Bad");
        root.Children.Add(errorText);
        Content = root;
        ShowButton(language);
        Loaded += (_, _) => Start();
        Unloaded += (_, _) => Stop();
    }

    public string? CameraName { get; }

    /// <summary>The JPEG taken, or null.</summary>
    public byte[]? Photo { get; private set; }

    public bool Validate()
    {
        if (Photo is not null)
        {
            return true;
        }

        errorText.Text = ReceptionText.TakePhotoFirst.For(UiLanguage.Current);
        errorText.Visibility = Visibility.Visible;
        return false;
    }

    private void Start()
    {
        if (sample)
        {
            camera.ShowSample();
            return;
        }

        CameraFeed.Shared.Acquire(this, CameraName, decodeQr: false);
        camera.Attach(CameraFeed.Shared);
        attached = true;
    }

    private void Stop()
    {
        if (attached)
        {
            camera.Detach();
            CameraFeed.Shared.Release(this);
            attached = false;
        }
    }

    private void TakeOrRetake()
    {
        var language = UiLanguage.Current;
        errorText.Visibility = Visibility.Collapsed;
        if (Photo is not null)
        {
            Photo = null;
            still.Visibility = Visibility.Collapsed;
            ShowButton(language);
            return;
        }

        var jpeg = sample ? null : CameraFeed.Shared.CaptureSquareJpeg();
        if (jpeg is null)
        {
            errorText.Text = ReceptionText.NoCameraPicture.For(language);
            errorText.Visibility = Visibility.Visible;
            return;
        }

        Photo = jpeg;
        still.Source = Images.FromBytes(jpeg);
        still.Visibility = Visibility.Visible;
        ShowButton(language);
    }

    private void ShowButton(AppLanguage language)
    {
        takeButton.Content = (Photo is null ? ReceptionText.TakePhoto : ReceptionText.Retake).For(language);
        takeButton.SetResourceReference(StyleProperty, Photo is null ? "Button.Primary" : "Button.Secondary");
    }
}
