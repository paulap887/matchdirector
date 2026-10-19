using System.Globalization;
using System.Text.Json;
using MatchDirector.Agents.Narration;
using MatchDirector.Contracts;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents.Recap;

/// <summary>A moment worth mentioning at full time, with the event it was detected on.</summary>
public sealed record KeyMoment(int Minute, string Type, string Team, string? Player, IReadOnlyDictionary<string, double> Metrics, IReadOnlyList<string> Evidence);

/// <summary>Everything a full-time report may say, all computed by code: final stats, key moments and player of the match.</summary>
public sealed record RecapFacts(MatchInfo Info, MatchSnapshot Final, IReadOnlyList<KeyMoment> KeyMoments, IReadOnlyList<PlayerRating> Ratings)
{
    public string HomeTeam => Info.Home.Name;
    public string AwayTeam => Info.Away.Name;
    public int HomeGoals => Final.Home.Goals;
    public int AwayGoals => Final.Away.Goals;

    public PlayerRating? Best => Ratings.FirstOrDefault();

    public string? Name(string playerId) =>
        Info.Home.Players.Concat(Info.Away.Players).FirstOrDefault(p => p.PlayerId == playerId)?.Name;

    public string TeamName(string teamId) => teamId == Info.Home.TeamId ? HomeTeam : AwayTeam;

    public IReadOnlyDictionary<string, double> Metrics => new Dictionary<string, double>
    {
        ["homeGoals"] = HomeGoals, ["awayGoals"] = AwayGoals,
        ["homeXg"] = Final.Home.Xg, ["awayXg"] = Final.Away.Xg,
        ["homeShots"] = Final.Home.Shots, ["awayShots"] = Final.Away.Shots,
        ["homeShotsOnTarget"] = Final.Home.ShotsOnTarget, ["awayShotsOnTarget"] = Final.Away.ShotsOnTarget,
        ["homePossession"] = Final.Home.Possession, ["awayPossession"] = Final.Away.Possession,
        ["homePassAccuracy"] = Final.Home.PassAccuracy, ["awayPassAccuracy"] = Final.Away.PassAccuracy,
        ["playerOfTheMatchRating"] = Best?.Rating ?? 0
    };

    public IReadOnlyList<string> Evidence => KeyMoments.SelectMany(m => m.Evidence).Distinct().ToList();

    /// <summary>The facts as the writer and the verifier see them.</summary>
    public string ToJson() => JsonSerializer.Serialize(new
    {
        venue = Info.Venue,
        score = new { home = HomeTeam, homeGoals = HomeGoals, away = AwayTeam, awayGoals = AwayGoals },
        fans = new { homeFan = HomeTeam, awayFan = AwayTeam },
        teams = new
        {
            home = new { name = HomeTeam, stats = Final.Home },
            away = new { name = AwayTeam, stats = Final.Away }
        },
        units = new { xg = "expected goals", possession = "percent", passAccuracy = "percent", rating = "match rating (higher is better)" },
        playerOfTheMatch = Best is { } best ? new { name = Name(best.PlayerId), team = TeamName(best.TeamId), rating = best.Rating } : null,
        topPlayers = Ratings.Take(3).Select(r => new
        {
            name = Name(r.PlayerId),
            team = TeamName(r.TeamId),
            r.Rating,
            stats = Final.Players.First(p => p.PlayerId == r.PlayerId)
        }),
        keyMoments = KeyMoments.Select(m => new { m.Minute, m.Type, m.Team, m.Player, m.Metrics })
    }, MatchDirectorJson.Options);

    /// <summary>
    /// Numbers a report may contain: everything in the facts. Whole numbers (goals, minutes, counts) are allowed only as
    /// themselves; decimals may be rounded or shown as percentages.
    /// </summary>
    public IReadOnlySet<double> AllowedNumbers()
    {
        var numbers = NarrationVerifier.NumbersIn(ToJson()).ToList();
        var allowed = NumberVariants.Expand(numbers.Where(n => n != Math.Floor(n)));
        allowed.UnionWith(numbers);
        return allowed;
    }

    public static RecapFacts From(MatchInfo info, MatchSnapshot final, IReadOnlyList<KeyMoment> keyMoments) =>
        new(info, final, keyMoments, PlayerRatings.Rank(final));
}

/// <summary>A full-time report for four audiences, plus a headline.</summary>
public sealed record RecapLines(string Headline, string Casual, string Analyst, string HomeFan, string AwayFan, string Source)
{
    public IEnumerable<string> All => [Headline, Casual, Analyst, HomeFan, AwayFan];
}

/// <summary>Deterministic full-time report, used without a model and whenever the model's report fails a check.</summary>
public static class TemplateRecap
{
    public static RecapLines Write(RecapFacts f)
    {
        string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        var (h, a) = (f.HomeGoals, f.AwayGoals);
        var score = $"{f.HomeTeam} {h}-{a} {f.AwayTeam}";
        var headline = h == a ? $"{f.HomeTeam} and {f.AwayTeam} draw {h}-{a}"
            : h > a ? $"{f.HomeTeam} beat {f.AwayTeam} {h}-{a}" : $"{f.AwayTeam} win {a}-{h} at {f.HomeTeam}";
        var best = f.Best is { } b ? $"{f.Name(b.PlayerId)} ({f.TeamName(b.TeamId)})" : null;
        var bestLine = best is null ? "" : $" Player of the match: {best}.";

        var casual = $"Full time at {f.Info.Venue}: {score}.{bestLine}";
        var analyst = $"{score}. xG {N(f.Final.Home.Xg)} v {N(f.Final.Away.Xg)}, shots {f.Final.Home.Shots} v {f.Final.Away.Shots} " +
                      $"({f.Final.Home.ShotsOnTarget} v {f.Final.Away.ShotsOnTarget} on target), possession " +
                      $"{N(f.Final.Home.Possession)}% v {N(f.Final.Away.Possession)}%.{bestLine}";

        string Fan(bool home)
        {
            var (us, them) = home ? (h, a) : (a, h);
            return us > them ? $"We win {us}-{them}! What a day.{bestLine}"
                : us < them ? $"Beaten {us}-{them}. We go again.{bestLine}"
                : $"A {us}-{them} draw. A point is a point.{bestLine}";
        }

        return new RecapLines(headline, casual, analyst, Fan(home: true), Fan(home: false), "template");
    }
}
