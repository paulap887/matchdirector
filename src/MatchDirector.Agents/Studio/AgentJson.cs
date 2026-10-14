using System.Text.Json;
using MatchDirector.Contracts;

namespace MatchDirector.Agents.Studio;

/// <summary>Reads the JSON object an agent was asked to reply with, tolerating markdown code fences.</summary>
public static class AgentJson
{
    public static T? Parse<T>(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return default;
        try
        {
            return JsonSerializer.Deserialize<T>(text.AsSpan(start, end - start + 1), MatchDirectorJson.Options);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
