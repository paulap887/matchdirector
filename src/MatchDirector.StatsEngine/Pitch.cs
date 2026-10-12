using MatchDirector.Contracts;

namespace MatchDirector.StatsEngine;

/// <summary>Pitch geometry. "Attacking frame" means the team attacks towards x = 105.</summary>
public static class Pitch
{
    public const double Length = 105, Width = 68, GoalWidth = 7.32;
    public static readonly PitchPoint GoalCentre = new(Length, Width / 2);

    public static double Distance(PitchPoint a, PitchPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Converts fixed pitch coordinates into the attacking frame of a team (see the MatchEvent direction convention).</summary>
    public static PitchPoint ToAttackingFrame(PitchPoint p, bool isHome, int period) =>
        isHome == (period % 2 == 1) ? p : new PitchPoint(Length - p.X, Width - p.Y);

    public static double DistanceToGoal(PitchPoint frame) => Distance(frame, GoalCentre);

    /// <summary>Angle in radians subtended by the goal mouth from a point in the attacking frame.</summary>
    public static double GoalAngle(PitchPoint frame)
    {
        var dx = Length - frame.X;
        var a = Math.Atan2(Width / 2 + GoalWidth / 2 - frame.Y, dx);
        var b = Math.Atan2(Width / 2 - GoalWidth / 2 - frame.Y, dx);
        return Math.Abs(a - b);
    }

    public static bool InFinalThird(PitchPoint frame) => frame.X >= Length * 2 / 3;
    public static bool InBox(PitchPoint frame) => frame.X >= Length - 16.5 && Math.Abs(frame.Y - Width / 2) <= 20.16;
}

/// <summary>Expected goals from shot location: a logistic model on distance and goal angle.</summary>
public static class ExpectedGoals
{
    public static double For(PitchPoint frame, bool underPressure)
    {
        var logit = -1.2 + 1.6 * Pitch.GoalAngle(frame) - 0.09 * Pitch.DistanceToGoal(frame) - (underPressure ? 0.3 : 0);
        return Math.Round(1 / (1 + Math.Exp(-logit)), 3);
    }
}
