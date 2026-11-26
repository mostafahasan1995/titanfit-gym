using System.Windows;

namespace Gym.UI.Shell;

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error,
}

public enum DialogTone
{
    Normal,

    /// <summary>Red primary button (delete, cancel a sale).</summary>
    Danger,

    /// <summary>Gold primary button (money: renew, charge).</summary>
    Gold,
}

/// <summary>A dialog shown over the app (inside the window, touch friendly). See <see cref="IShellUi.ShowDialogAsync"/>.</summary>
public sealed class DialogRequest
{
    public required LocalText Title { get; init; }

    /// <summary>Text under the title.</summary>
    public LocalText? Message { get; init; }

    /// <summary>A form or any other content under the message.</summary>
    public FrameworkElement? Content { get; init; }

    public LocalText PrimaryText { get; init; } = CommonText.Ok;

    /// <summary>The second button (e.g. Cancel). Null = only the primary button. Esc presses it.</summary>
    public LocalText? SecondaryText { get; init; } = CommonText.Cancel;

    public DialogTone Tone { get; init; } = DialogTone.Normal;

    /// <summary>
    /// Runs when the primary button is pressed (with the busy look on the button). Return false to keep the dialog
    /// open, e.g. after showing a validation error in the content.
    /// </summary>
    public Func<Task<bool>>? OnPrimary { get; init; }

    public double Width { get; init; } = 520;
}

/// <summary>Shell services for the pages: <see cref="PageContext.Ui"/>.</summary>
public interface IShellUi
{
    /// <summary>A short message at the bottom that goes away by itself.</summary>
    void Toast(LocalText message, ToastKind kind = ToastKind.Success);

    void Toast(string message, ToastKind kind = ToastKind.Success);

    /// <summary>Shows the error as a red toast (nothing for an expired login: the shell goes back to the login).</summary>
    void ShowError(Exception error);

    /// <summary>True when the primary button closed it, false for the secondary button or Esc.</summary>
    Task<bool> ShowDialogAsync(DialogRequest request);

    Task ShowMessageAsync(LocalText title, LocalText message);

    Task<bool> ConfirmAsync(LocalText title, LocalText message, LocalText? confirmText = null, bool danger = false);

    /// <summary>Runs <paramref name="work"/> with the full-window busy overlay.</summary>
    Task<T> RunBusyAsync<T>(Func<Task<T>> work, LocalText? message = null);

    Task RunBusyAsync(Func<Task> work, LocalText? message = null);
}
