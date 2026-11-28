namespace Gym.Infrastructure.Services;

internal static class Dto
{
    /// <summary>Relative URL of a member photo (resolve it against the server address). Null without a photo.</summary>
    public static string? PhotoUrl(Guid memberId, string? photoFile) =>
        photoFile is null ? null : $"api/members/{memberId}/photo?v={Uri.EscapeDataString(Path.GetFileNameWithoutExtension(photoFile))}";

    public static SubscriptionStatusDto? Status(DateOnly? endDate, DateOnly today, int expiringSoonDays)
    {
        if (endDate is not { } end)
        {
            return null;
        }

        var state = SubscriptionRules.GetStatus(end, today, expiringSoonDays);
        return new SubscriptionStatusDto(state.Status, end, state.DaysLeft, state.DaysOverdue);
    }
}

/// <summary>Adds up payments per currency (<see cref="CurrencyTotals"/>).</summary>
public static class CurrencyTotalsOf
{
    /// <param name="payments">Each payment: its currency, its dollar value and the amount paid in that currency.</param>
    public static CurrencyTotals From(IEnumerable<(Currency Currency, decimal Usd, decimal Paid)> payments)
    {
        ArgumentNullException.ThrowIfNull(payments);
        decimal usd = 0, syp = 0, sypAsUsd = 0;
        int usdCount = 0, sypCount = 0;
        foreach (var (currency, value, paid) in payments)
        {
            if (currency == Currency.Syp)
            {
                syp += paid;
                sypAsUsd += value;
                sypCount++;
            }
            else
            {
                usd += value;
                usdCount++;
            }
        }

        return new CurrencyTotals(usd, syp, sypAsUsd, usdCount, sypCount);
    }
}
