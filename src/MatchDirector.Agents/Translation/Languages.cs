namespace MatchDirector.Agents.Translation;

public sealed record Language(string Code, string Name);

/// <summary>
/// The languages the studio can broadcast in besides English. Only left-to-right languages, so the overlay layout never
/// changes; adding one is a line here and a dictionary in the web app.
/// </summary>
public static class Languages
{
    public static readonly IReadOnlyList<Language> Supported =
    [
        new("fr", "French"),
        new("es", "Spanish"),
        new("de", "German"),
        new("pt", "Portuguese"),
        new("it", "Italian")
    ];

    public static Language? Find(string code) => Supported.FirstOrDefault(l => l.Code == code);
}
