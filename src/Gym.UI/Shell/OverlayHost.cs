using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Gym.UI.Controls;

namespace Gym.UI.Shell;

/// <summary>
/// The layer above the login screen and the shell: toasts at the bottom, dialogs in the middle, the busy overlay.
/// Everything stays inside the window (touch friendly, and the smoke screenshots can render it).
/// </summary>
public sealed class OverlayHost : Grid, IShellUi
{
    private const int MaxToasts = 3;
    private static readonly TimeSpan ToastTime = TimeSpan.FromSeconds(4);

    private readonly StackPanel toasts;
    private readonly Grid dialogs;
    private readonly LoadingOverlay busy;
    private readonly List<(Grid Layer, TaskCompletionSource<bool> Result)> open = [];
    private int busyCount;

    public OverlayHost()
    {
        toasts = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(24, 24, 24, 32) };
        dialogs = new Grid();
        busy = new LoadingOverlay();
        Children.Add(dialogs);
        Children.Add(busy);
        Children.Add(toasts);
    }

    /// <summary>True while a dialog is open.</summary>
    public bool HasDialog => open.Count > 0;

    public void Toast(LocalText message, ToastKind kind = ToastKind.Success) => Toast(message.Current, kind);

    public void Toast(string message, ToastKind kind = ToastKind.Success)
    {
        var toast = new ToastView { Kind = kind, Message = message };
        toasts.Children.Add(toast);
        while (toasts.Children.Count > MaxToasts)
        {
            toasts.Children.RemoveAt(0);
        }

        var timer = new DispatcherTimer { Interval = ToastTime };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            toasts.Children.Remove(toast);
        };
        timer.Start();
    }

    public void ShowError(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (!ErrorText.IsSessionExpired(error))
        {
            Toast(ErrorText.Describe(error, UiLanguage.Current), ToastKind.Error);
        }
    }

    public Task<bool> ShowDialogAsync(DialogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = new TaskCompletionSource<bool>();
        var previousFocus = Keyboard.FocusedElement;

        var primary = new Button { Content = request.PrimaryText.Current, MinWidth = 140, Margin = new Thickness(12, 0, 0, 0) };
        primary.SetResourceReference(StyleProperty, request.Tone switch
        {
            DialogTone.Danger => "Button.Danger",
            DialogTone.Gold => "Button.Gold",
            _ => "Button.Primary",
        });
        Button? secondary = null;
        var footer = new StackPanel { Orientation = Orientation.Horizontal };
        if (request.SecondaryText is { } secondaryText)
        {
            secondary = new Button { Content = secondaryText.Current, MinWidth = 120 };
            secondary.SetResourceReference(StyleProperty, "Button.Secondary");
            footer.Children.Add(secondary);
        }
        else
        {
            primary.Margin = new Thickness(0);
        }

        footer.Children.Add(primary);

        var frame = new DialogFrame
        {
            Title = request.Title.Current,
            Message = request.Message?.Current,
            Content = request.Content,
            Footer = footer,
            Width = request.Width,
            MaxHeight = 820,
        };
        var layer = new Grid();
        layer.SetResourceReference(BackgroundProperty, "Brush.Scrim");
        layer.Children.Add(frame);

        void Close(bool value)
        {
            if (!dialogs.Children.Contains(layer))
            {
                return;
            }

            dialogs.Children.Remove(layer);
            open.RemoveAll(o => ReferenceEquals(o.Layer, layer));
            if (previousFocus is not null && open.Count == 0)
            {
                Keyboard.Focus(previousFocus);
            }

            result.TrySetResult(value);
        }

        async Task PressPrimaryAsync()
        {
            if (!primary.IsEnabled)
            {
                return;
            }

            if (request.OnPrimary is { } onPrimary)
            {
                primary.IsEnabled = false;
                if (secondary is not null)
                {
                    secondary.IsEnabled = false;
                }

                bool ok;
                try
                {
                    ok = await onPrimary().ConfigureAwait(true);
                }
#pragma warning disable CA1031 // Any failure keeps the dialog open and shows the error.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    ShowError(ex);
                    ok = false;
                }
                finally
                {
                    primary.IsEnabled = true;
                    if (secondary is not null)
                    {
                        secondary.IsEnabled = true;
                    }
                }

                if (!ok)
                {
                    return;
                }
            }

            Close(true);
        }

        primary.Click += async (_, _) => await PressPrimaryAsync().ConfigureAwait(true);
        if (secondary is not null)
        {
            secondary.Click += (_, _) => Close(false);
        }

        layer.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Escape && secondary is not null)
            {
                e.Handled = true;
                Close(false);
            }
            else if (e.Key == Key.Enter && !e.Handled && e.OriginalSource is not TextBox { AcceptsReturn: true })
            {
                e.Handled = true;
                await PressPrimaryAsync().ConfigureAwait(true);
            }
        };

        open.Add((layer, result));
        dialogs.Children.Add(layer);
        frame.Loaded += (_, _) => _ = Dispatcher.InvokeAsync(
            () =>
            {
                if (!frame.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)))
                {
                    primary.Focus();
                }
            },
            DispatcherPriority.Input);
        return result.Task;
    }

    public async Task ShowMessageAsync(LocalText title, LocalText message) =>
        await ShowDialogAsync(new DialogRequest { Title = title, Message = message, PrimaryText = CommonText.Ok, SecondaryText = null }).ConfigureAwait(true);

    public Task<bool> ConfirmAsync(LocalText title, LocalText message, LocalText? confirmText = null, bool danger = false) =>
        ShowDialogAsync(new DialogRequest
        {
            Title = title,
            Message = message,
            PrimaryText = confirmText ?? CommonText.Confirm,
            SecondaryText = CommonText.Cancel,
            Tone = danger ? DialogTone.Danger : DialogTone.Normal,
        });

    public async Task<T> RunBusyAsync<T>(Func<Task<T>> work, LocalText? message = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        BeginBusy(message);
        try
        {
            return await work().ConfigureAwait(true);
        }
        finally
        {
            EndBusy();
        }
    }

    public async Task RunBusyAsync(Func<Task> work, LocalText? message = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        BeginBusy(message);
        try
        {
            await work().ConfigureAwait(true);
        }
        finally
        {
            EndBusy();
        }
    }

    /// <summary>Closes every dialog (as cancelled), the toasts and the busy overlay, e.g. at logout.</summary>
    public void CloseAll()
    {
        foreach (var (_, result) in open.ToList())
        {
            result.TrySetResult(false);
        }

        open.Clear();
        dialogs.Children.Clear();
        toasts.Children.Clear();
        busyCount = 0;
        busy.IsActive = false;
    }

    private void BeginBusy(LocalText? message)
    {
        busyCount++;
        busy.Message = (message ?? CommonText.PleaseWait).Current;
        busy.IsActive = true;
    }

    private void EndBusy()
    {
        busyCount = Math.Max(0, busyCount - 1);
        busy.IsActive = busyCount > 0;
    }
}
