using System.Reflection;

namespace Gym.Api;

/// <summary>The server version shown by <c>GET /health</c>, e.g. <c>0.1.0+f9c17cc</c>.</summary>
internal static class AppVersion
{
    public static string Current { get; } = Read();

    private static string Read()
    {
        var assembly = typeof(AppVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
        {
            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        // The SDK appends "+<full commit id>". Keep the version and a short commit id.
        var plus = informational.IndexOf('+');
        return plus >= 0 && informational.Length > plus + 8 ? informational[..(plus + 8)] : informational;
    }
}
