using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Gym.Client;
using Gym.Contracts;
using Gym.UI;
using Gym.UI.Controls;

namespace Gym.Cafeteria.Sales;

/// <summary>One item sold in a shift.</summary>
public sealed record SoldItem(ProductSalesDto Item, AppLanguage Language)
{
    public string Name => Receipts.ReceiptLayout.ProductName(Item.NameEn, Item.NameAr, Language);

    public string QuantityText => NumberText.Format(Item.Quantity);

    public decimal Total => Item.Total;
}

/// <summary>A shift in the "Past shifts" list, with its totals.</summary>
public sealed record ShiftListItem(ShiftSummaryDto Summary, AppLanguage Language)
{
    public int Id => Summary.Shift.Id;

    public bool IsOpen => Summary.Shift.ClosedAt is null;

    /// <summary>"26 Nov 2026" in the local time zone.</summary>
    public string Day => DateText.ShortDate(DateOnly.FromDateTime(Summary.Shift.OpenedAt.ToLocalTime()), Language);

    /// <summary>"08:00 – 22:05" (or "08:00" while open).</summary>
    public string Hours => Summary.Shift.ClosedAt is { } closed
        ? $"{DateText.Time(Summary.Shift.OpenedAt)} – {DateText.Time(closed)}"
        : DateText.Time(Summary.Shift.OpenedAt);

    public string Station => Summary.Shift.Station;

    public string Cashier => Summary.Shift.CashierName;

    public decimal Total => Summary.SalesTotal;

    public string SalesText => Summary.SaleCount == 1
        ? CafeteriaText.OneSale.For(Language)
        : BidiText.FixNumbers(CafeteriaText.SaleCount.Format(Language, NumberText.Format(Summary.SaleCount)), Language);

    public PillTone StatusTone => IsOpen ? PillTone.Good : Summary.CashDifference is < 0 ? PillTone.Bad : PillTone.Neutral;

    public string StatusText => (IsOpen ? CafeteriaText.ShiftStillOpen : Summary.CashDifference is < 0 ? CafeteriaText.CashShort : CafeteriaText.ShiftClosedState).For(Language);
}

/// <summary>
/// The shift-close screen without WPF controls: the cashier's open shift with its summary (sales, totals, items
/// sold, expected cash), closing it with the counted cash, opening one, and the past shifts with their totals.
/// A cashier sees his own shifts; an admin sees everyone's.
/// </summary>
public sealed class ShiftViewModel : ObservableObject
{
    public const int HistoryDays = 14;

    private readonly IGymApi api;
    private readonly Func<string> station;
    private readonly int? cashierId;
    private AppLanguage language;
    private ShiftSummaryDto? current;
    private ShiftSummaryDto? shown;

    /// <param name="cashierId">Only this cashier's shifts (null = everyone's, for an admin).</param>
    public ShiftViewModel(IGymApi api, Func<string> station, int? cashierId, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(station);
        this.api = api;
        this.station = station;
        this.cashierId = cashierId;
        this.language = language;
    }

    /// <summary>The logged-in cashier's open shift, or null.</summary>
    public ShiftSummaryDto? Current
    {
        get => current;
        private set => SetProperty(ref current, value);
    }

    /// <summary>The shift on the left: the open one, or a past one picked in the list.</summary>
    public ShiftSummaryDto? Shown
    {
        get => shown;
        private set => SetProperty(ref shown, value);
    }

    /// <summary>The last <see cref="HistoryDays"/> days of shifts, newest first, with their totals.</summary>
    public ObservableCollection<ShiftListItem> History { get; } = [];

    public AppLanguage Language
    {
        get => language;
        set
        {
            if (SetProperty(ref language, value))
            {
                var items = History.Select(h => h with { Language = value }).ToList();
                History.Clear();
                items.ForEach(History.Add);
            }
        }
    }

    /// <summary>Counted minus expected: negative = cash missing, positive = extra cash, null = not counted.</summary>
    public static decimal? Difference(decimal expectedCash, decimal? countedCash) => countedCash - expectedCash;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var open = await api.GetCurrentShiftAsync(ct).ConfigureAwait(true);
        Current = open is null ? null : await api.GetShiftSummaryAsync(open.Id, ct).ConfigureAwait(true);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var shifts = await api.GetShiftsAsync(today.AddDays(1 - HistoryDays), today, cashierId, ct).ConfigureAwait(true);
        var items = new List<ShiftListItem>();
        foreach (var shift in shifts.OrderByDescending(s => s.OpenedAt).Take(30))
        {
            var summary = Current is { } openSummary && openSummary.Shift.Id == shift.Id ? openSummary : await api.GetShiftSummaryAsync(shift.Id, ct).ConfigureAwait(true);
            items.Add(new ShiftListItem(summary, language));
        }

        History.Clear();
        items.ForEach(History.Add);
        var keep = Shown is { } before ? items.FirstOrDefault(i => i.Id == before.Shift.Id)?.Summary : null;
        Shown = Current ?? keep ?? items.FirstOrDefault()?.Summary;
    }

    /// <summary>Shows a past (or the open) shift on the left.</summary>
    public async Task ShowAsync(int shiftId, CancellationToken ct = default)
    {
        Shown = History.FirstOrDefault(h => h.Id == shiftId)?.Summary ?? await api.GetShiftSummaryAsync(shiftId, ct).ConfigureAwait(true);
    }

    /// <summary>Closes the open shift with the counted cash (optional) and a note. Returns its final summary.</summary>
    public async Task<ShiftSummaryDto> CloseAsync(decimal? countedCash, string? note, CancellationToken ct = default)
    {
        var open = Current ?? throw new InvalidOperationException("No shift is open.");
        var closed = await api.CloseShiftAsync(open.Shift.Id, new CloseShiftRequest(countedCash, string.IsNullOrWhiteSpace(note) ? null : note.Trim()), ct)
            .ConfigureAwait(true);
        await LoadAsync(ct).ConfigureAwait(true);
        Shown = closed;
        return closed;
    }

    /// <summary>Opens a shift with the cash already in the drawer (a sale also opens one by itself).</summary>
    public async Task<ShiftDto> OpenAsync(decimal openingCash, CancellationToken ct = default)
    {
        var shift = await api.OpenShiftAsync(new OpenShiftRequest(station(), openingCash), ct).ConfigureAwait(true);
        await LoadAsync(ct).ConfigureAwait(true);
        return shift;
    }
}
