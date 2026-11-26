using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gym.UI.Station;

/// <summary>
/// Settings of one PC (station), shared by the TITANFIT apps on it. Stored as JSON in
/// <c>%ProgramData%\TitanFit\station.json</c> (see <see cref="StationSettingsStore"/>); the station installer can
/// write the same file. Unknown or missing values fall back to the defaults.
/// </summary>
public sealed record StationSettings
{
    public const string DefaultServerUrl = "http://localhost:5080";
    public const int DefaultPort = 5080;

    /// <summary>The API server (the admin PC), e.g. <c>http://192.168.1.10:5080</c>.</summary>
    public string ServerUrl { get; init; } = DefaultServerUrl;

    /// <summary>Sent with check-ins, sales and shifts and shown in the sidebar, e.g. "Reception 1". Empty = the app's default.</summary>
    public string? StationName { get; init; }

    /// <summary>UI language at start. The EN / عربي switch saves the last choice here.</summary>
    [JsonConverter(typeof(LanguageCodeConverter))]
    public AppLanguage Language { get; init; } = AppLanguage.English;

    /// <summary>The webcam to use (Reception), by its Windows name. Null = the first one.</summary>
    public string? CameraName { get; init; }

    /// <summary>The receipt printer (Cafeteria), by its Windows name. Null = the default printer.</summary>
    public string? PrinterName { get; init; }

    /// <summary>The last username that logged in here (filled in on the login screen; never for the Admin app).</summary>
    public string? LastUsername { get; init; }

    /// <summary>The server as a URI with a trailing slash, for <see cref="System.Net.Http.HttpClient.BaseAddress"/>.</summary>
    [JsonIgnore]
    public Uri ServerUri => TryNormalizeServerUrl(ServerUrl, out var url) ? new Uri(url + "/") : new Uri(DefaultServerUrl + "/");

    /// <summary>
    /// Cleans up what the user typed: adds <c>http://</c> when there is no scheme and the port 5080 when the
    /// address has no port and no scheme ("192.168.1.10" → "http://192.168.1.10:5080"). Drops a trailing slash.
    /// False when it is not an http(s) address.
    /// </summary>
    public static bool TryNormalizeServerUrl(string? input, out string url)
    {
        url = "";
        var text = input?.Trim() ?? "";
        if (text.Length == 0)
        {
            return false;
        }

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            var hostPart = text.Split('/')[0];
            text = "http://" + text;
            if (!hostPart.Contains(':', StringComparison.Ordinal))
            {
                var slash = text.IndexOf('/', "http://".Length);
                text = slash < 0
                    ? text + ":" + DefaultPort.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : text.Insert(slash, ":" + DefaultPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        url = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return true;
    }

    /// <summary>Writes the language as "en" or "ar".</summary>
    private sealed class LanguageCodeConverter : JsonConverter<AppLanguage>
    {
        public override AppLanguage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                reader.Skip();
                return AppLanguage.English;
            }

            try
            {
                return AppLanguages.Parse(reader.GetString() ?? "en");
            }
            catch (ArgumentException)
            {
                return AppLanguage.English;
            }
        }

        public override void Write(Utf8JsonWriter writer, AppLanguage value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            writer.WriteStringValue(value.ToCode());
        }
    }
}

/// <summary>Loads and saves <see cref="StationSettings"/> as JSON.</summary>
public sealed class StationSettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // Arabic stays readable in the file
    };

    /// <summary><c>%ProgramData%\TitanFit\station.json</c>.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TitanFit", "station.json");

    public string FilePath { get; } = filePath;

    /// <summary>False on the first run (no file yet): the app asks for the server address.</summary>
    public bool Exists => File.Exists(FilePath);

    /// <summary>The saved settings, or the defaults when the file is missing or unreadable.</summary>
    public StationSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new StationSettings();
            }

            var settings = JsonSerializer.Deserialize<StationSettings>(File.ReadAllText(FilePath), Json) ?? new StationSettings();
            return string.IsNullOrWhiteSpace(settings.ServerUrl) ? settings with { ServerUrl = StationSettings.DefaultServerUrl } : settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new StationSettings();
        }
    }

    /// <summary>Writes the file (creating the folder). Throws <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>.</summary>
    public void Save(StationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var folder = Path.GetDirectoryName(Path.GetFullPath(FilePath));
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
        File.Move(temp, FilePath, overwrite: true);
    }

    /// <summary>Like <see cref="Save"/> but never throws; returns false when the file could not be written.</summary>
    public bool TrySave(StationSettings settings)
    {
        try
        {
            Save(settings);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
