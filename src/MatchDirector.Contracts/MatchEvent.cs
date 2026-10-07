namespace MatchDirector.Contracts;

/// <summary>A single synthetic match event. Pitch coordinates are metres on a 105 x 68 pitch.</summary>
public sealed record MatchEvent(
    string EventId,
    string MatchId,
    int Sequence,
    TimeSpan MatchClock,
    int Period,
    MatchEventType Type,
    string TeamId,
    string? PlayerId,
    PitchPoint Start,
    PitchPoint? End = null,
    string? TargetPlayerId = null,
    EventOutcome Outcome = EventOutcome.None,
    bool UnderPressure = false);

public readonly record struct PitchPoint(double X, double Y);

public enum MatchEventType
{
    KickOff,
    Pass,
    Carry,
    Shot,
    Tackle,
    Interception,
    Pressure,
    PossessionChange,
    Foul,
    Goal,
    Save,
    PeriodEnd
}

public enum EventOutcome
{
    None,
    Success,
    Failure
}
