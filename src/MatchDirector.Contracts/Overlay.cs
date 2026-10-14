namespace MatchDirector.Contracts;

/// <summary>Timed, machine-readable overlay the rendering layer can draw over a live feed.</summary>
/// <remarks>
/// <see cref="Variants"/> holds one line per audience (casual, analyst, homeFan, awayFan).
/// <see cref="Participants"/> lists every player involved, main player first, for player-focus views.
/// </remarks>
public sealed record Overlay(
    string OverlayId,
    string MatchId,
    TimeSpan MatchClock,
    TimeSpan ShowAt,
    int DurationMs,
    string Type,
    int Priority,
    string? PlayerId,
    IReadOnlyDictionary<string, double> Metrics,
    IReadOnlyList<string> Evidence,
    IReadOnlyDictionary<string, string> Variants,
    IReadOnlyList<string>? Participants = null);
