using System.ComponentModel;
using System.Net.Http;
using Gym.Client;
using Gym.Contracts;
using Gym.Domain;

namespace Gym.UI;

/// <summary>
/// The exchange rate in use on this station (Syrian pounds for 1 US dollar), shared by every page and control. The
/// window loads it at login, keeps it current from the live updates and reloads it every few minutes (a rate from
/// yesterday turns stale after midnight). Change it on the UI thread only.
/// <para>
/// No rate (<see cref="HasRate"/> false): amounts in pounds are hidden and only dollars can be taken. A rate from an
/// earlier day (<see cref="IsStale"/>) is still used, with a warning banner.
/// </para>
/// </summary>
public sealed class ExchangeRates : INotifyPropertyChanged
{
    private ExchangeRateStatusDto? status;

    /// <summary>The one used by the controls (<see cref="Controls.SypText"/>, <see cref="Controls.CurrencyPicker"/>) and the shell.</summary>
    public static ExchangeRates Current { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised after every change (also when the same values come again).</summary>
    public event EventHandler? Changed;

    /// <summary>What the server said last, or null before it was loaded (or after logout).</summary>
    public ExchangeRateStatusDto? Status => status;

    public bool IsLoaded => status is not null;

    /// <summary>The newest rate entered, or null.</summary>
    public ExchangeRateDto? Rate => status?.Rate;

    /// <summary>Pounds for 1 dollar, or null when no rate exists.</summary>
    public decimal? SypPerUsd => status?.Rate?.SypPerUsd;

    public bool HasRate => SypPerUsd is > 0;

    /// <summary>
    /// <see cref="ExchangeRateState.Today"/>, <see cref="ExchangeRateState.Stale"/> or
    /// <see cref="ExchangeRateState.None"/> (also before it was loaded). The later of the server's and this PC's day counts.
    /// </summary>
    public ExchangeRateState State
    {
        get
        {
            if (status is null)
            {
                return ExchangeRateState.None;
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            return CurrencyRules.GetState(status.Rate?.Date, status.Today > today ? status.Today : today);
        }
    }

    public bool IsStale => State == ExchangeRateState.Stale;

    /// <summary>Amounts in pounds are rounded to a multiple of this (default 1).</summary>
    public int RoundingStep => status?.SypRoundingStep ?? CurrencyRules.DefaultSypRoundingStep;

    /// <summary>The pounds for <paramref name="usd"/> at the rate, rounded; null without a rate.</summary>
    public decimal? ToSyp(decimal usd) => SypPerUsd is { } rate && rate > 0 ? CurrencyRules.ToSyp(usd, rate, RoundingStep) : null;

    /// <summary>
    /// What the customer pays for <paramref name="usd"/> in <paramref name="currency"/>: the dollars, or the rounded
    /// pounds. Pounds without a rate fall back to dollars (the pickers do not offer pounds then).
    /// </summary>
    public PaymentAmount Due(decimal usd, Currency currency) =>
        currency == Currency.Syp && SypPerUsd is { } rate && rate > 0
            ? new PaymentAmount(Currency.Syp, CurrencyRules.ToSyp(usd, rate, RoundingStep), rate)
            : new PaymentAmount(Currency.Usd, usd, SypPerUsd);

    /// <summary>The payment to send: the currency and the rate shown (so the server uses the same amount in pounds).</summary>
    public PaymentRequest Payment(Currency currency) =>
        new(currency == Currency.Syp && HasRate ? Currency.Syp : Currency.Usd, Rate?.Id);

    public void Set(ExchangeRateStatusDto? value)
    {
        status = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Loads the rate. A failure keeps the last one (the server may be restarting); a lost login is passed on.</summary>
    public async Task RefreshAsync(IGymApi api, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(api);
        try
        {
            Set(await api.GetExchangeRateAsync(ct).ConfigureAwait(true));
        }
        catch (GymApiException ex) when (!ErrorText.IsSessionExpired(ex))
        {
        }
        catch (HttpRequestException)
        {
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
        }
    }
}
