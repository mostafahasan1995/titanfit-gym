using Gym.UI;
using Gym.UI.Station;

namespace Gym.Tests.Foundation;

public sealed class StationSettingsTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "titanfit-station-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(folder, "TitanFit", "station.json");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Missing_file_means_first_run_with_the_defaults()
    {
        var store = new StationSettingsStore(FilePath);

        var settings = store.Load();

        Assert.False(store.Exists);
        Assert.Equal("http://localhost:5080", settings.ServerUrl);
        Assert.Equal(new Uri("http://localhost:5080/"), settings.ServerUri);
        Assert.Equal(AppLanguage.English, settings.Language);
        Assert.Null(settings.StationName);
        Assert.Null(settings.LastUsername);
    }

    [Fact]
    public void Save_creates_the_folder_and_load_reads_the_same_settings()
    {
        var store = new StationSettingsStore(FilePath);
        var settings = new StationSettings
        {
            ServerUrl = "http://192.168.1.10:5080",
            StationName = "Reception 2",
            Language = AppLanguage.Arabic,
            CameraName = "USB Camera",
            PrinterName = "XP-80C",
            LastUsername = "sara",
        };

        store.Save(settings);

        Assert.True(store.Exists);
        Assert.Equal(settings, store.Load());
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void The_file_is_plain_json_with_the_language_as_en_or_ar()
    {
        var store = new StationSettingsStore(FilePath);

        store.Save(new StationSettings { Language = AppLanguage.Arabic, StationName = "الاستقبال 1" });

        var json = File.ReadAllText(FilePath);
        Assert.Contains("\"serverUrl\": \"http://localhost:5080\"", json, StringComparison.Ordinal);
        Assert.Contains("\"language\": \"ar\"", json, StringComparison.Ordinal);
        Assert.Equal("الاستقبال 1", store.Load().StationName);
    }

    [Fact]
    public void A_file_written_by_the_installer_with_only_the_server_works()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, """{ "serverUrl": "http://10.0.0.5:5080", "language": "fr" }""");

        var settings = new StationSettingsStore(FilePath).Load();

        Assert.Equal("http://10.0.0.5:5080", settings.ServerUrl);
        Assert.Equal(AppLanguage.English, settings.Language);
        Assert.Null(settings.PrinterName);
    }

    [Fact]
    public void A_broken_file_gives_the_defaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ not json");

        var settings = new StationSettingsStore(FilePath).Load();

        Assert.Equal(new StationSettings(), settings);
    }

    [Theory]
    [InlineData("192.168.1.10", "http://192.168.1.10:5080")]
    [InlineData("admin-pc", "http://admin-pc:5080")]
    [InlineData("admin-pc:6000", "http://admin-pc:6000")]
    [InlineData("http://admin-pc:5080/", "http://admin-pc:5080")]
    [InlineData("  https://gym.local  ", "https://gym.local")]
    [InlineData("HTTP://Admin-PC:5080", "http://admin-pc:5080")]
    public void Server_addresses_are_cleaned_up(string input, string expected)
    {
        Assert.True(StationSettings.TryNormalizeServerUrl(input, out var url));
        Assert.Equal(expected, url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://admin-pc")]
    [InlineData("http://")]
    public void Bad_server_addresses_are_refused(string input)
    {
        Assert.False(StationSettings.TryNormalizeServerUrl(input, out _));
    }

    [Fact]
    public void The_server_uri_ends_with_a_slash_for_the_http_client()
    {
        var settings = new StationSettings { ServerUrl = "http://192.168.1.10:5080" };

        Assert.Equal("http://192.168.1.10:5080/", settings.ServerUri.ToString());
    }
}
