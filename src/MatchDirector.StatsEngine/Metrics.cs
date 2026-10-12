using MatchDirector.Contracts;

namespace MatchDirector.StatsEngine;

public sealed record PassMetrics(double Distance, double? Speed, double Progression, bool Progressive, bool IntoBox, double Difficulty)
{
    /// <summary>
    /// Difficulty 0..1 blends length, pressure, how far it moves the ball towards goal and whether it lands in the box.
    /// Progressive follows the common definition: the ball ends at least 25% closer to goal and travels at least 10 m.
    /// </summary>
    public static PassMetrics From(MatchEvent pass, PitchPoint start, PitchPoint end)
    {
        var distance = Pitch.Distance(start, end);
        var before = Pitch.DistanceToGoal(start);
        var progression = before - Pitch.DistanceToGoal(end);
        var intoBox = Pitch.InBox(end) && !Pitch.InBox(start);
        var difficulty = 0.35 * Math.Min(distance / 40, 1)
                       + (pass.UnderPressure ? 0.25 : 0)
                       + 0.2 * Math.Clamp(progression / 20, 0, 1)
                       + (intoBox ? 0.2 : 0);
        return new PassMetrics(
            Math.Round(distance, 1),
            BallSpeed(distance, pass.Duration),
            Math.Round(progression, 1),
            progression >= 0.25 * before && distance >= 10,
            intoBox,
            Math.Round(Math.Clamp(difficulty, 0, 1), 2));
    }

    internal static double? BallSpeed(double distance, TimeSpan? duration) =>
        duration is { TotalSeconds: > 0 } d ? Math.Round(distance / d.TotalSeconds, 1) : null;
}

public sealed record ShotMetrics(double Distance, double Angle, double Xg, double? Speed)
{
    public static ShotMetrics From(MatchEvent shot, PitchPoint start)
    {
        var flight = shot.End is { } end ? Pitch.Distance(shot.Start, end) : 0;
        return new ShotMetrics(
            Math.Round(Pitch.DistanceToGoal(start), 1),
            Math.Round(Pitch.GoalAngle(start) * 180 / Math.PI, 1),
            ExpectedGoals.For(start, shot.UnderPressure),
            PassMetrics.BallSpeed(flight, shot.Duration));
    }
}
