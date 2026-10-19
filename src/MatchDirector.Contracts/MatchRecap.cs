namespace MatchDirector.Contracts;

/// <summary>
/// The full-time report for a match. <see cref="Variants"/> holds one report per audience (casual, analyst, homeFan,
/// awayFan, source). Every number comes from <see cref="Metrics"/>; <see cref="Evidence"/> lists the events behind the
/// key moments it describes, and <see cref="Trace"/> records how it was written and checked.
/// </summary>
public sealed record MatchRecap(
    string MatchId,
    string Headline,
    IReadOnlyDictionary<string, string> Variants,
    string? PlayerOfTheMatch,
    IReadOnlyDictionary<string, double> Metrics,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<AgentStep>? Trace = null);
