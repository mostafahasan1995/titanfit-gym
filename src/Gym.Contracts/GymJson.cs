using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gym.Contracts;

/// <summary>JSON settings used on both sides: camelCase names and enums as strings.</summary>
public static class GymJson
{
    public static JsonSerializerOptions Options { get; } = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
