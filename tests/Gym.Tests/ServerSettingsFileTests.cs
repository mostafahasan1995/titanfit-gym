using Gym.Api;
using Microsoft.Extensions.Configuration;

namespace Gym.Tests;

/// <summary>The installer's server.json: read on top of appsettings.json, still overridden by env vars and args.</summary>
public sealed class ServerSettingsFileTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "titanfit-settings-" + Guid.NewGuid().ToString("N"));
    private readonly string envName = "TITANFIT_TEST_" + Guid.NewGuid().ToString("N") + "__Value";

    public ServerSettingsFileTests() => Directory.CreateDirectory(folder);

    private string FilePath => Path.Combine(folder, ServerSettingsFile.FileName);

    [Fact]
    public void Default_path_is_server_json_in_the_data_folder()
    {
        Assert.Equal(ServerSettingsFile.FileName, Path.GetFileName(ServerSettingsFile.DefaultPath));
    }

    [Fact]
    public void Missing_file_adds_nothing()
    {
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Gym"] = "from-appsettings" });
        var sources = configuration.Sources.Count;

        configuration.AddServerSettingsFile([], FilePath);

        Assert.Equal("from-appsettings", configuration.GetConnectionString("Gym"));
        Assert.Equal(sources, configuration.Sources.Count);
    }

    [Fact]
    public void File_overrides_appsettings()
    {
        File.WriteAllText(FilePath, """
            { "ConnectionStrings": { "Gym": "Host=localhost;Port=5433" }, "Storage": { "DataPath": "C:\\Data" } }
            """);
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Gym"] = "" });

        configuration.AddServerSettingsFile([], FilePath);

        Assert.Equal("Host=localhost;Port=5433", configuration.GetConnectionString("Gym"));
        Assert.Equal(@"C:\Data", configuration["Storage:DataPath"]);
    }

    [Fact]
    public void Environment_variables_and_command_line_still_win()
    {
        var key = envName.Replace("__", ":", StringComparison.Ordinal);
        File.WriteAllText(FilePath, $$"""{ "{{key.Split(':')[0]}}": { "Value": "file" }, "Other": "file" }""");
        Environment.SetEnvironmentVariable(envName, "env");
        try
        {
            var configuration = new ConfigurationManager();

            configuration.AddServerSettingsFile(["--Other=args"], FilePath);

            Assert.Equal("env", configuration[key]);
            Assert.Equal("args", configuration["Other"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envName, null);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
