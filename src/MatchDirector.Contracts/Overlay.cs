namespace MatchDirector.Contracts;

/// <summary>Timed, machine-readable overlay the rendering layer can draw over a live feed.</summary>
/// <remarks>
/// <see cref="Variants"/> holds one line per audience (casual, analyst, homeFan, awayFan).
/// <see cref="Participants"/> lists every player involved, main player first, for player-focus views.
/// <see cref="Trace"/> records what each agent and code check did, so viewers can see how the overlay was made.
/// An overlay may be published again with the same <see cref="OverlayId"/> once the review agents have finished.
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
    IReadOnlyList<string>? Participants = null,
    IReadOnlyList<AgentStep>? Trace = null);

/// <summary>One step in producing an overlay: which agent or check ran, on what model, how long it took and the outcome.</summary>
public sealed record AgentStep(string Agent, string Model, string Outcome, int DurationMs, string? Detail = null);
