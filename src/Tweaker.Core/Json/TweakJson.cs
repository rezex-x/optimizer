using System.Text.Json;
using System.Text.Json.Serialization;
using Tweaker.Core.Models;

namespace Tweaker.Core.Json;

public static class TweakJson
{
    public static JsonSerializerOptions Options { get; } = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    public static List<Tweak> DeserializeCatalog(string json)
        => JsonSerializer.Deserialize<List<Tweak>>(json, Options) ?? new List<Tweak>();

    public static string SerializeCatalog(IEnumerable<Tweak> tweaks)
        => JsonSerializer.Serialize(tweaks, Options);
}
