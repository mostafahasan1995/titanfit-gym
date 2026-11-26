using System.Windows.Controls;

namespace Gym.UI.Shell;

/// <summary>
/// Base class for the app pages (a UserControl with the <see cref="PageContext"/>). In XAML the root element is
/// <c>&lt;shell:PageView x:Class="Gym.Reception.Pages.CheckInPage" ...&gt;</c> and the constructor is
/// <c>public CheckInPage(PageContext context) : base(context) { InitializeComponent(); }</c>.
/// </summary>
public class PageView : UserControl, IShellPage
{
    protected PageView(PageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Context = context;
    }

    public PageContext Context { get; }

    public virtual Task OnShownAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public virtual void OnLanguageChanged()
    {
    }
}
