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

    /// <summary>What the named player did in this moment, so a writer never mixes up, say, the goalkeeper and the shooter.</summary>
    public string? PlayerRole => Player is null ? null : Moment.Type switch
    {
        MomentType.Goal => "scorer",
        MomentType.BigChanceMissed => "shooter who missed",
        MomentType.KeySave => "goalkeeper who made the save (not the shooter)",
        MomentType.DifficultPass => "passer",
        MomentType.PassingMove => "player who made the last pass of the move",
        MomentType.HighRegain => "player who won the ball back",
        MomentType.RocketShot => "shooter",
        MomentType.DrivingRun or MomentType.DistanceMilestone => "ball carrier",
        _ => null
    };

    /// <summary>
    /// How a supporter of one club should feel about this moment, decided by code so fan lines are written from the
    /// right side: whose moment it is, and the mood (celebrate, frustrated, worried, relieved or neutral).
    /// </summary>
    public FanView FanViewFor(bool home)
    {
        var club = home ? HomeTeam : AwayTeam;
        var (us, them) = home ? (HomeGoals, AwayGoals) : (AwayGoals, HomeGoals);
        switch (Moment.Type)
        {
            case MomentType.HalfTime or MomentType.FullTime:
                return new FanView(club, "the whole match", us > them ? "celebrate" : us < them ? "frustrated" : "neutral");
            case MomentType.TempoShift:
                return new FanView(club, "the whole match", "neutral");
        }

        var ours = Team == club;
        var goodForOwner = Moment.Type != MomentType.BigChanceMissed;
        var mood = (ours, goodForOwner) switch
        {
            (false, true) when Moment.Type == MomentType.RocketShot => "relieved", // a thunderous shot that did not go in
            (true, true) => "celebrate",
            (true, false) => "frustrated",
            (false, true) => "worried",
            (false, false) => "relieved"
        };
        return new FanView(club, ours ? "your club's moment" : "the other club's moment", mood);
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

/// <param name="Club">The club this fan supports.</param>
/// <param name="Moment">Whose moment it is from the fan's side: "your club's moment", "the other club's moment" or "the whole match".</param>
/// <param name="Mood">celebrate, frustrated, worried, relieved or neutral.</param>
public sealed record FanView(string Club, string Moment, string Mood);
