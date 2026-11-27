using System.Net.Http;
using System.Windows.Media;
using Gym.Client;
using Gym.UI;
using Gym.UI.Controls;
using Gym.UI.Shell;

namespace Gym.Reception.Logic;

/// <summary>Server errors of the reception screens in the staff's words (falls back to <see cref="ErrorText"/>).</summary>
public static class ReceptionErrors
{
    public static string Describe(Exception error, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error is GymApiException { Code: { } code } && Text(code) is { } text ? text.For(language) : ErrorText.Describe(error, language);
    }

    /// <summary>A red toast (nothing for an expired login: the shell goes back to the login).</summary>
    public static void Show(PageContext context, Exception error)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(error);
        if (!ErrorText.IsSessionExpired(error))
        {
            context.Ui.Toast(Describe(error, context.Language), ToastKind.Error);
        }
    }

    private static LocalText? Text(string code) => code switch
    {
        "trainer_has_clients" => ReceptionText.ErrorTrainerHasClients,
        "cannot_link_self" => ReceptionText.ErrorCannotLinkSelf,
        "client_not_linked" => ReceptionText.ErrorClientNotLinked,
        "photo_too_large" => ReceptionText.ErrorPhotoTooLarge,
        "unsupported_photo_type" or "invalid_photo" => ReceptionText.ErrorPhotoInvalid,
        "plan_not_found" or "plan_inactive" => ReceptionText.ErrorPlanGone,
        "member_not_found" => ReceptionText.ErrorMemberGone,
        _ => null,
    };
}

/// <summary>Member photos from the server, kept for the session (by URL; a new photo has a new URL).</summary>
public static class PhotoCache
{
    private const int MaxPhotos = 300;
    private static readonly Dictionary<string, ImageSource?> Photos = new(StringComparer.Ordinal);

    /// <summary>The photo at <paramref name="url"/>, or null when there is none or it cannot be loaded.</summary>
    public static async Task<ImageSource?> GetAsync(IGymApi api, string? url, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(api);
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (Photos.TryGetValue(url, out var cached))
        {
            return cached;
        }

        try
        {
            var image = Images.FromBytes(await api.GetBytesAsync(url, ct).ConfigureAwait(true), 320);
            if (Photos.Count >= MaxPhotos)
            {
                Photos.Clear();
            }

            Photos[url] = image;
            return image;
        }
        catch (Exception ex) when (ex is GymApiException or HttpRequestException or TimeoutException)
        {
            return null;
        }
    }
}
