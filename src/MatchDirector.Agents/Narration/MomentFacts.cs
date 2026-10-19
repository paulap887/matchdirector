using MatchDirector.Contracts;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents.Narration;

/// <summary>Everything a narrator may say about a moment. Nothing outside these facts is allowed in the output.</summary>
public sealed record MomentFacts(
    Moment Moment,
    int Minute,
    string Team,
    string Opponent,
    string? Player,
    int HomeGoals,
    int AwayGoals,
    string HomeTeam,
    string AwayTeam)
{
    public static MomentFacts From(Moment moment, MatchInfo info, MatchSnapshot snapshot)
    {
        var isHome = moment.TeamId == info.Home.TeamId;
        var team = isHome ? info.Home : info.Away;
        var opponent = isHome ? info.Away : info.Home;
        var player = team.Players.FirstOrDefault(p => p.PlayerId == moment.PlayerId)?.Name;
        return new MomentFacts(
            moment, (int)moment.MatchClock.TotalMinutes + 1, team.Name, opponent.Name, player,
            snapshot.Home.Goals, snapshot.Away.Goals, info.Home.Name, info.Away.Name);
    }

    /// <summary>
    /// Every number a line about this moment may contain, in any rounding a writer might reasonably use.
    /// <paramref name="extra"/> adds numbers returned by code tools (for the analyst's insight).
    /// </summary>
    public IReadOnlySet<double> AllowedNumbers(IEnumerable<double>? extra = null)
    {
        // Minute and score are allowed only as themselves; metrics may be rounded or shown as percentages.
        var numbers = NumberVariants.Expand(Moment.Metrics.Values.Concat(extra ?? []));
        numbers.UnionWith([Minute, HomeGoals, AwayGoals]);
        return numbers;
    }
}
