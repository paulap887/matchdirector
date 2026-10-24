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
    FullTime,
    RocketShot,
    DrivingRun,
    DistanceMilestone
}

/// <summary>
/// Predefined thresholds that trigger speed and distance indicators. Moments that cross one carry the threshold as a
/// metric (speedThreshold), so viewers can see which line was crossed.
/// </summary>
public static class Thresholds
{
    /// <summary>A shot at or above this ball speed (metres per second) is a rocket.</summary>
    public const double RocketShotSpeed = 30;

    /// <summary>A carry at or above this speed (metres per second) and <see cref="DrivingRunMinDistance"/> is a driving run.</summary>
    public const double DrivingRunSpeed = 6.9;

    public const double DrivingRunMinDistance = 12;

    /// <summary>Players are announced each time their total distance carried passes a multiple of this (metres).</summary>
    public const double CarryMilestone = 150;
}

/// <summary>
/// Something worth telling the viewer, detected by code. Every number in <see cref="Metrics"/> was computed here,
/// and <see cref="Evidence"/> lists the event IDs it was computed from, so agents can narrate but never invent.
/// <see cref="Participants"/> are the players named in those events (actors and pass receivers), main player first.
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
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Participants);
