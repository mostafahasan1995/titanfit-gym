using System.Globalization;
using System.Net;
using System.Windows;
using Gym.Admin.Controls;
using Gym.Admin.Logic;
using Gym.Client;
using Gym.Contracts;
using Gym.UI;
using Gym.UI.Controls;
using Gym.UI.Shell;
using Microsoft.Win32;

namespace Gym.Admin.Pages;

public static class BackupsText
{
    public static LocalText BackUpNow { get; } = new("Back up now", "نسخ احتياطي الآن");

    public static LocalText BackingUp { get; } = new("Backing up…", "جارٍ النسخ…");

    public static LocalText StatusOk { get; } = new("Backups are up to date", "النسخ الاحتياطي محدّث");

    public static LocalText StatusFailed { get; } = new("The last backup failed", "فشل آخر نسخ احتياطي");

    public static LocalText StatusOld { get; } = new("No backup for more than 2 days", "لا يوجد نسخ احتياطي منذ أكثر من يومين");

    public static LocalText StatusNever { get; } = new("No backup yet", "لا يوجد نسخ احتياطي بعد");

    public static LocalText LastGood { get; } = new("Last backup: {0} · {1}", "آخر نسخة: {0} · {1}");

    public static LocalText LastNone { get; } = new("Press Back up now to make the first one.", "اضغط «نسخ احتياطي الآن» لإنشاء أول نسخة.");

    public static LocalText Next { get; } = new("Next daily backup: {0}", "النسخ اليومي التالي: {0}");

    public static LocalText FailedAt { get; } = new("Failed {0}: {1}", "فشل {0}: {1}");

    public static LocalText Done { get; } = new("Backup saved ({0}).", "تم حفظ النسخة ({0}).");

    public static LocalText FilesTitle { get; } = new("Backup files", "ملفات النسخ الاحتياطي");

    public static LocalText FilesSummary { get; } = new("{0} backups · {1} in total", "عدد النسخ {0} · الحجم الكلي {1}");

    public static LocalText File { get; } = new("File", "الملف");

    public static LocalText Made { get; } = new("Made", "التاريخ");

    public static LocalText Size { get; } = new("Size", "الحجم");

    public static LocalText NoFiles { get; } = new("No backup files here", "لا توجد ملفات نسخ احتياطي هنا");

    public static LocalText NoFilesText { get; } = new("If the backup drive is unplugged, connect it.", "إذا كان قرص النسخ الاحتياطي مفصولًا فقم بوصله.");

    public static LocalText SettingsTitle { get; } = new("Backup settings", "إعدادات النسخ الاحتياطي");

    public static LocalText Folder { get; } = new("Backup folder on the server PC", "مجلد النسخ على جهاز الخادم");

    public static LocalText FolderHint { get; } = new(
        "Use a folder on the external backup drive. It must exist; the server checks that it can write there.",
        "استخدم مجلدًا على قرص النسخ الاحتياطي الخارجي. يجب أن يكون موجودًا، ويتحقق الخادم من إمكانية الكتابة فيه.");

    public static LocalText Browse { get; } = new("Browse…", "استعراض…");

    public static LocalText UseDefault { get; } = new("Use the default folder", "استخدام المجلد الافتراضي");

    public static LocalText Time { get; } = new("Daily at", "يوميًا الساعة");

    public static LocalText Keep { get; } = new("Keep the last", "الاحتفاظ بآخر");

    public static LocalText PgDump { get; } = new("pg_dump program (leave empty for the installed one)", "برنامج pg_dump (اتركه فارغًا لاستخدام المثبّت)");

    public static LocalText PgDumpMissing { get; } = new("Not found on the server PC.", "غير موجود على جهاز الخادم.");

    public static LocalText RestoreTitle { get; } = new("Restore a backup", "استعادة نسخة احتياطية");

    public static LocalText RestoreText { get; } = new(
        "Restoring replaces all data and is done on the server PC: open PowerShell as administrator and run the line below with the file you want. It saves the current data first.",
        "الاستعادة تستبدل كل البيانات وتتم على جهاز الخادم: افتح PowerShell كمسؤول وشغّل السطر التالي مع الملف المطلوب. يحفظ البيانات الحالية أولًا.");
}

/// <summary>Backups: status, Back up now, the files and the settings. The server does the work (pg_dump).</summary>
public partial class BackupsPage : PageView
{
    private const string RestoreLine = @"& 'C:\Program Files\TitanFit\Setup\restore-backup.ps1' -BackupFile '<folder>\titanfit-YYYYMMDD-HHmm.dump'";
    private const string FolderExample = @"E:\TitanFit-Backups";

    private BackupStatusDto? status;
    private IReadOnlyList<BackupFileDto> files = [];
    private BackupSettingsDto? settings;
    private bool running;

    public BackupsPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        ThemeAssist.SetPlaceholder(FolderBox, FolderExample);
        BrowseButton.IsEnabled = Context.IsSample || IsLocalServer(Context.Api.BaseAddress);
    }

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        settings = await Context.Api.GetBackupSettingsAsync(cancellationToken).ConfigureAwait(true);
        FillForm();
        await LoadStatusAsync(cancellationToken).ConfigureAwait(true);
    }

    public override void OnLanguageChanged()
    {
        ShowStatus();
        ShowFiles();
        ShowSettingsHints();
    }

    private async Task LoadStatusAsync(CancellationToken ct)
    {
        status = await Context.Api.GetBackupStatusAsync(ct).ConfigureAwait(true);
        files = await Context.Api.GetBackupsAsync(ct).ConfigureAwait(true);
        ShowStatus();
        ShowFiles();
    }

    private void ShowStatus()
    {
        var language = Context.Language;
        BackupNowButton.Content = (running ? BackupsText.BackingUp : BackupsText.BackUpNow).For(language);
        BackupNowButton.IsEnabled = !running && status?.IsRunning != true;
        if (status is not { } s)
        {
            return;
        }

        var (title, soft, strong, icon) = s.Warning switch
        {
            BackupWarning.None => (BackupsText.StatusOk, "Brush.GoodSoft", "Brush.Good", UiIcons.Check),
            BackupWarning.LastFailed => (BackupsText.StatusFailed, "Brush.BadSoft", "Brush.Bad", UiIcons.Alert),
            BackupWarning.TooOld => (BackupsText.StatusOld, "Brush.WarningSoft", "Brush.Warning", UiIcons.Clock),
            _ => (BackupsText.StatusNever, "Brush.WarningSoft", "Brush.Warning", UiIcons.Info),
        };
        StatusTitle.Text = title.For(language);
        StatusIconBox.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, soft);
        StatusIcon.SetResourceReference(Icon.ForegroundProperty, strong);
        StatusIcon.Data = icon;
        StatusLast.Text = s.LastSuccess is { } good
            ? BackupsText.LastGood.Format(language, DateText.DateAndTime(good.FinishedAt, language), SizeText.Format(good.SizeBytes ?? 0, language))
            : BackupsText.LastNone.For(language);
        StatusNext.Text = BackupsText.Next.Format(language, DateText.DateAndTime(s.NextRunAt, language));

        if (s.LastRun is { Succeeded: false } failed)
        {
            var reason = AdminErrors.ForCode(failed.ErrorCode)?.For(language) ?? AdminErrors.BackupFailed.For(language);
            StatusError.Text = BackupsText.FailedAt.Format(language, DateText.DateAndTime(failed.StartedAt, language), reason)
                + (string.IsNullOrWhiteSpace(failed.Error) ? "" : Environment.NewLine + failed.Error);
            StatusError.FlowDirection = language == AppLanguage.Arabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            StatusError.Visibility = Visibility.Visible;
        }
        else
        {
            StatusError.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowFiles()
    {
        var language = Context.Language;
        FilesGrid.ItemsSource = files.Select(f => new FileRow(f.Name, DateText.DateAndTime(f.CreatedAt, language), SizeText.Format(f.SizeBytes, language))).ToList();
        FilesSummary.Text = BackupsText.FilesSummary.Format(language, NumberText.Format(files.Count), SizeText.Format(files.Sum(f => f.SizeBytes), language));
        FilesFolder.Text = status?.Folder ?? settings?.Folder ?? "";
        FilesEmpty.Visibility = files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FilesGrid.Visibility = files.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void FillForm()
    {
        if (settings is not { } s)
        {
            return;
        }

        FolderBox.Text = s.IsDefaultFolder ? "" : s.Folder;
        var times = BackupTimes.Choices(s.Time).Select(t => new Choice(t, BackupTimes.Format(t))).ToList();
        TimeBox.ItemsSource = times;
        TimeBox.SelectedItem = times.First(c => Equals(c.Value, new TimeOnly(s.Time.Hour, s.Time.Minute)));
        KeepBox.Text = s.KeepLast.ToString(CultureInfo.InvariantCulture);
        PgDumpBox.Text = s.PgDumpPath ?? "";
        FolderField.Error = KeepField.Error = PgDumpField.Error = null;
        ShowSettingsHints();
        RestoreCommand.Text = RestoreLine;
    }

    private void ShowSettingsHints()
    {
        if (settings is not { } s)
        {
            return;
        }

        // The default folder is the placeholder when none is chosen; pg_dump shows the one the server found.
        ThemeAssist.SetPlaceholder(FolderBox, s.DefaultFolder);
        ThemeAssist.SetPlaceholder(PgDumpBox, s.ResolvedPgDumpPath ?? "pg_dump");
        PgDumpField.Hint = s.ResolvedPgDumpPath is null ? BackupsText.PgDumpMissing.For(Context.Language) : null;
    }

    private async void OnBackupNowClick(object sender, RoutedEventArgs e)
    {
        if (running)
        {
            return;
        }

        var language = Context.Language;
        running = true;
        ShowStatus();
        try
        {
            var run = await Context.Api.RunBackupAsync().ConfigureAwait(true);
            if (run.Succeeded)
            {
                Context.Ui.Toast(BackupsText.Done.Format(language, run.FileName ?? ""));
            }
            else
            {
                Context.Ui.Toast(AdminErrors.ForCode(run.ErrorCode)?.For(language) ?? AdminErrors.BackupFailed.For(language), ToastKind.Error);
            }
        }
#pragma warning disable CA1031 // Show any failure as a toast; the status below is reloaded.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.Toast(AdminErrors.Describe(ex, language), ToastKind.Error);
        }
        finally
        {
            running = false;
        }

        try
        {
            await LoadStatusAsync(CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.ShowError(ex);
            ShowStatus();
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var language = Context.Language;
        var (keep, keepError) = FormChecks.WholeNumber(KeepBox.Text, 1, 365);
        FolderField.Error = PgDumpField.Error = null;
        KeepField.Error = keepError?.For(language);
        if (keep is not { } keepLast || TimeBox.SelectedItem is not Choice { Value: TimeOnly time })
        {
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            settings = await Context.Api.UpdateBackupSettingsAsync(
                new SaveBackupSettingsRequest(FormChecks.Optional(FolderBox.Text), time, keepLast, FormChecks.Optional(PgDumpBox.Text))).ConfigureAwait(true);
            FillForm();
            Context.Ui.Toast(CommonText.SettingsSaved);
            await LoadStatusAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (GymApiException ex)
        {
            var message = AdminErrors.Describe(ex, language);
            switch (ex.Code)
            {
                case "backup_folder_missing" or "backup_folder_invalid" or "backup_folder_not_writable":
                    FolderField.Error = message;
                    break;
                case "invalid_backup_keep":
                    KeepField.Error = message;
                    break;
                case "pg_dump_not_found":
                    PgDumpField.Error = message;
                    break;
                default:
                    Context.Ui.Toast(message, ToastKind.Error);
                    break;
            }
        }
#pragma warning disable CA1031 // Show any other failure as a toast.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.ShowError(ex);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        if (Context.IsSample)
        {
            return;
        }

        var dialog = new OpenFolderDialog { Title = BackupsText.Folder.For(Context.Language) };
        var current = FormChecks.Optional(FolderBox.Text) ?? settings?.DefaultFolder;
        if (current is not null && System.IO.Directory.Exists(current))
        {
            dialog.InitialDirectory = current;
        }

        var owner = Window.GetWindow(this);
        if ((owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) == true)
        {
            FolderBox.Text = dialog.FolderName;
            FolderField.Error = null;
        }
    }

    private void OnDefaultClick(object sender, RoutedEventArgs e)
    {
        FolderBox.Text = "";
        FolderField.Error = null;
    }

    /// <summary>The Browse button picks a folder on this PC, so it only makes sense when the server runs here.</summary>
    private static bool IsLocalServer(Uri? server)
    {
        if (server is null)
        {
            return false;
        }

        if (server.IsLoopback || string.Equals(server.Host, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            var local = Dns.GetHostAddresses(Dns.GetHostName());
            return IPAddress.TryParse(server.Host, out var address) && local.Contains(address);
        }
        catch (System.Net.Sockets.SocketException)
        {
            return false;
        }
    }

    /// <summary>A row of the backup files list.</summary>
    public sealed record FileRow(string Name, string Made, string Size);
}
