namespace MatchDirector.Contracts;

/// <summary>Timed, machine-readable overlay the rendering layer can draw over a live feed.</summary>
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
    IReadOnlyDictionary<string, string> Variants);
