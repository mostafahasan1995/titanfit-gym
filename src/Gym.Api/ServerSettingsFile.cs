using Gym.Infrastructure.Storage;

namespace Gym.Api;

/// <summary>
/// The machine settings written by the server installer: <c>%ProgramData%\TitanFit\server.json</c> with the
/// connection string (generated database password) and the data path. Only SYSTEM and Administrators can read it.
/// </summary>
internal static class ServerSettingsFile
{
    public const string FileName = "server.json";

    public static string DefaultPath => Path.Combine(StorageOptions.DefaultDataPath(), FileName);

    /// <summary>
    /// Adds the file on top of appsettings.json when it exists. Environment variables and the command line are added
    /// again after it, so they still override it.
    /// </summary>
    public static void AddServerSettingsFile(this IConfigurationBuilder configuration, string[] args, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        path ??= DefaultPath;
        if (!File.Exists(path))
        {
            return;
        }

        configuration.AddJsonFile(path, optional: true, reloadOnChange: false);
        configuration.AddEnvironmentVariables();
        configuration.AddCommandLine(args);
    }
}
