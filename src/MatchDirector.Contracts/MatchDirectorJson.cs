using System.Text.Json;
using System.Text.Json.Serialization;

namespace MatchDirector.Contracts;

/// <summary>Serializer settings shared by every service that reads or writes contract types.</summary>
public static class MatchDirectorJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
