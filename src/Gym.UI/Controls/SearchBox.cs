using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace Gym.UI.Controls;

/// <summary>
/// A search field with an icon and a clear button. <see cref="SearchChanged"/> fires 300 ms after the last key
/// (or at once on Enter, or when cleared), so a page can query the API without a request per key.
/// </summary>
public sealed class SearchBox : TextBox
{
    private readonly DispatcherTimer debounce;
    private ButtonBase? clearButton;

    static SearchBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SearchBox), new FrameworkPropertyMetadata(typeof(SearchBox)));
    }

    public SearchBox()
    {
        debounce = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(300) };
        debounce.Tick += (_, _) => Raise();
    }

    /// <summary>The text to search for changed (debounced). Read <see cref="TextBox.Text"/>.</summary>
    public event EventHandler? SearchChanged;

    public override void OnApplyTemplate()
    {
        if (clearButton is not null)
        {
            clearButton.Click -= OnClear;
        }

        base.OnApplyTemplate();
        clearButton = GetTemplateChild("PART_ClearButton") as ButtonBase;
        if (clearButton is not null)
        {
            clearButton.Click += OnClear;
        }
    }

    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);
        debounce.Stop();
        debounce.Start();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Key == Key.Enter)
        {
            Raise();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && Text.Length > 0)
        {
            Clear();
            Raise();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        Clear();
        Focus();
        Raise();
    }

    private void Raise()
    {
        debounce.Stop();
        SearchChanged?.Invoke(this, EventArgs.Empty);
    }
}
