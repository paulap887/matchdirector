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

    /// <summary>Every number the narration is allowed to contain.</summary>
    public IReadOnlySet<double> AllowedNumbers()
    {
        var numbers = new HashSet<double> { Minute, HomeGoals, AwayGoals };
        foreach (var value in Moment.Metrics.Values)
        {
            numbers.Add(value);
            numbers.Add(Math.Round(value));
            numbers.Add(Math.Round(value, 1));
            numbers.Add(Math.Round(value, 2));
            if (value is > 0 and <= 1)
                numbers.Add(Math.Round(value * 100)); // 0.34 xG may be said as 34%
        }
        return numbers;
    }
}
