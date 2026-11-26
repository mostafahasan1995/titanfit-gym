namespace Gym.Infrastructure.Storage;

/// <summary>Where the server keeps its files (member photos, the token signing key). Section "Storage".</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Empty = %ProgramData%\TitanFit on Windows, ./data next to the program elsewhere.</summary>
    public string? DataPath { get; set; }

    public string ResolvedDataPath => string.IsNullOrWhiteSpace(DataPath) ? DefaultDataPath() : DataPath;

    public string PhotosPath => Path.Combine(ResolvedDataPath, "photos");

    public string KeysPath => Path.Combine(ResolvedDataPath, "keys");

    public static string DefaultDataPath() =>
        OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TitanFit")
            : Path.Combine(AppContext.BaseDirectory, "data");
}
