namespace MatchDirector.StatsEngine;

public sealed record PlayerRating(string PlayerId, string TeamId, double Rating);

/// <summary>
/// A simple, transparent match rating, so "player of the match" is decided by code rather than by a model:
/// 3 per goal, 2 per expected goal, 0.15 per progressive pass, 0.3 per tackle won or interception,
/// 0.02 per completed pass and 0.005 per metre carried.
/// </summary>
public static class PlayerRatings
{
    public static double Rate(PlayerStats p) => Math.Round(
        3 * p.Goals + 2 * p.Xg + 0.15 * p.ProgressivePasses + 0.3 * (p.Tackles + p.Interceptions)
        + 0.02 * p.PassesCompleted + 0.005 * p.DistanceCarried, 1);

    /// <summary>Players ordered best first; ties go to the player with more goals, then by ID for determinism.</summary>
    public static IReadOnlyList<PlayerRating> Rank(MatchSnapshot snapshot) =>
        snapshot.Players
            .OrderByDescending(Rate)
            .ThenByDescending(p => p.Goals)
            .ThenBy(p => p.PlayerId, StringComparer.Ordinal)
            .Select(p => new PlayerRating(p.PlayerId, p.TeamId, Rate(p)))
            .ToList();
}
