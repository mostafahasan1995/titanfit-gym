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
