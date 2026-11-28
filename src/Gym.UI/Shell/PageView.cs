using System.Windows;
using System.Windows.Controls;

namespace Gym.UI.Shell;

/// <summary>
/// Base class for the app pages (a UserControl with the <see cref="PageContext"/>). In XAML the root element is
/// <c>&lt;shell:PageView x:Class="Gym.Reception.Pages.CheckInPage" ...&gt;</c> and the constructor is
/// <c>public CheckInPage(PageContext context) : base(context) { InitializeComponent(); }</c>.
/// </summary>
public class PageView : UserControl, IShellPage
{
    /// <summary>
    /// Below this size the whole page scrolls (the safety net for very small windows). It is less than the page area
    /// at 1366 x 768 with 125 % scaling, so a page must fit that area with its own scrolling lists and docked action bars.
    /// </summary>
    public const double DefaultMinWidth = 720;

    public const double DefaultMinHeight = 400;

    protected PageView(PageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
        MinWidth = DefaultMinWidth;
        MinHeight = DefaultMinHeight;
    }

    public PageContext Context { get; }

    public virtual Task OnShownAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public virtual void OnLanguageChanged()
    {
    }
}
