namespace MatchDirector.Agents.Translation;

/// <summary>A translation of one item's English lines, kept only while those lines stay the same.</summary>
public sealed record Translated(string Language, IReadOnlyDictionary<string, string> Source, IReadOnlyDictionary<string, string> Lines, IReadOnlyList<Contracts.AgentStep> Trace);

/// <summary>
/// The latest version of each item on air (an overlay or a full-time report) with its translations. The review lane may
/// republish an item with new English lines while a translation is in flight; a translation of the old lines is then
/// discarded, so a viewer never reads a translation of words that are no longer on screen.
/// </summary>
public sealed class TranslationBook<T>(Func<T, string> matchId, Func<T, IReadOnlyDictionary<string, string>> english, int kept)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Entry> _entries = [];
    private readonly LinkedList<string> _order = [];

    /// <summary>Records the latest version of an item, keeping translations whose English is unchanged.</summary>
    public void Put(string id, T item)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                entry.Item = item;
                return;
            }
            _entries[id] = new Entry(item);
            _order.AddLast(id);
            if (_order.Count > kept)
            {
                _entries.Remove(_order.First!.Value);
                _order.RemoveFirst();
            }
        }
    }

    /// <summary>Stores a translation if the item still shows the English it was made from.</summary>
    public bool Add(string id, Translated translation)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(id, out var entry) || !Same(english(entry.Item), translation.Source))
                return false;
            entry.Translations[translation.Language] = translation;
            return true;
        }
    }

    /// <summary>The item, its English lines and the languages it still needs, or null if it has left the book.</summary>
    public (T Item, IReadOnlyDictionary<string, string> English, IReadOnlyList<Language> Missing)? Pending(string id, IEnumerable<Language> languages)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(id, out var entry))
                return null;
            var lines = english(entry.Item);
            return (entry.Item, lines, languages.Where(l => !Current(entry, l.Code, lines)).ToList());
        }
    }

    /// <summary>The latest version of an item with the translations that match it.</summary>
    public (T Item, IReadOnlyList<Translated> Translations)? Current(string id)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(id, out var entry))
                return null;
            var lines = english(entry.Item);
            return (entry.Item, entry.Translations.Values.Where(t => Same(lines, t.Source)).OrderBy(t => t.Language).ToList());
        }
    }

    /// <summary>The items of a match, newest first, for translating what a viewer can still see after choosing a language.</summary>
    public IReadOnlyList<string> Ids(string match)
    {
        lock (_lock)
            return _order.Reverse().Where(id => matchId(_entries[id].Item) == match).ToList();
    }

    private static bool Current(Entry entry, string language, IReadOnlyDictionary<string, string> lines) =>
        entry.Translations.TryGetValue(language, out var t) && Same(lines, t.Source);

    private static bool Same(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b) =>
        a.Count == b.Count && a.All(kv => b.TryGetValue(kv.Key, out var v) && v == kv.Value);

    private sealed class Entry(T item)
    {
        public T Item { get; set; } = item;
        public Dictionary<string, Translated> Translations { get; } = [];
    }
}
