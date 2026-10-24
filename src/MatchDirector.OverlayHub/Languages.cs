using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MatchDirector.OverlayHub;

/// <summary>
/// The language each connected viewer is watching in. The agent service asks for the distinct languages, so it only
/// translates into languages someone is actually reading.
/// </summary>
public sealed partial class ViewerLanguages
{
    private readonly ConcurrentDictionary<string, string> _byConnection = new();

    public void Set(string connectionId, string language)
    {
        if (language == "en" || !Code().IsMatch(language))
            _byConnection.TryRemove(connectionId, out _);
        else
            _byConnection[connectionId] = language;
    }

    public void Remove(string connectionId) => _byConnection.TryRemove(connectionId, out _);

    public IReadOnlyList<string> Watched() => _byConnection.Values.Distinct().Order().ToList();

    [GeneratedRegex("^[a-z]{2}$")]
    private static partial Regex Code();
}
