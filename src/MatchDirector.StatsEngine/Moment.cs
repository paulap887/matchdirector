namespace MatchDirector.StatsEngine;

public enum MomentType
{
    Goal,
    BigChanceMissed,
    KeySave,
    PassingMove,
    DifficultPass,
    HighRegain,
    MomentumShift,
    TempoShift,
    HalfTime,
    FullTime
}

/// <summary>
/// Something worth telling the viewer, detected by code. Every number in <see cref="Metrics"/> was computed here,
/// and <see cref="Evidence"/> lists the event IDs it was computed from, so agents can narrate but never invent.
/// </summary>
public sealed record Moment(
    string MomentId,
    MomentType Type,
    TimeSpan MatchClock,
    int Period,
    string TeamId,
    string? PlayerId,
    int Priority,
    IReadOnlyDictionary<string, double> Metrics,
    IReadOnlyList<string> Evidence);
