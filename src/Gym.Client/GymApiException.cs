using System.Net;
using System.Text.Json;

namespace Gym.Client;

/// <summary>The server refused a request. <see cref="Code"/> is the stable error code (e.g. <c>insufficient_stock</c>).</summary>
public sealed class GymApiException(HttpStatusCode statusCode, string? code, string message, string? body = null)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string? Code { get; } = code;

    /// <summary>The raw problem details JSON, for extra fields such as <c>shortages</c>.</summary>
    public string? Body { get; } = body;

    public static async Task<GymApiException> FromResponseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(response);
        string? body = null;
        string? title = null;
        string? code = null;
        try
        {
            body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var json = JsonDocument.Parse(body);
                if (json.RootElement.ValueKind == JsonValueKind.Object)
                {
                    title = ReadString(json.RootElement, "title");
                    code = ReadString(json.RootElement, "code");
                }
            }
        }
        catch (JsonException)
        {
            // Not a problem details body; keep the status code.
        }

        var message = title ?? $"The server answered {(int)response.StatusCode} {response.ReasonPhrase}.";
        return new GymApiException(response.StatusCode, code, message, body);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
