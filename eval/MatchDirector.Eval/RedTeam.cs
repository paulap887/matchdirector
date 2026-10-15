using MatchDirector.Agents.Narration;
using MatchDirector.Agents.Studio;
using MatchDirector.Simulator.Engine;
using MatchDirector.StatsEngine;

namespace MatchDirector.Eval;

/// <summary>One kind of deliberately wrong narration, and which checker is expected to catch it.</summary>
public sealed record Attack(string Name, string Description, string ExpectedCatcher, Func<MomentFacts, Narrated, Narrated?> Apply);

public sealed record AttackResult(string Name, string Description, string ExpectedCatcher, int Samples, int CaughtByCode, int CaughtByCritic, int Caught);

public sealed record RedTeamResult(int Moments, IReadOnlyList<AttackResult> Attacks, int CleanSamples, int CleanFlaggedByCritic, bool CriticEvaluated, IReadOnlyList<string> FalseAlarms);

/// <summary>
/// Feeds deliberately wrong lines to the two checkers. Clean lines are the deterministic template narration, which is
/// known to be correct, so any critic flag on them is a false alarm.
/// </summary>
public static class RedTeam
{
    public static readonly IReadOnlyList<Attack> Attacks =
    [
        new("Invented statistic", "adds a season total that was never computed", "code",
            (f, n) => n with { Analyst = $"{n.Analyst} That makes it 37 in 113 games this season." }),
        new("Wrong team", "credits the moment to the other club", "critic",
            (f, n) => n.Casual.Contains(f.Team) ? n with { Casual = n.Casual.Replace(f.Team, f.Opponent) } : null),
        new("Swapped fan lines", "home supporters cheer for the away club and vice versa", "critic",
            (f, n) => n.HomeFan == n.AwayFan ? null : n with { HomeFan = n.AwayFan, AwayFan = n.HomeFan }),
        new("Wrong score story", "says the wrong club is ahead", "critic",
            (f, n) => n with { Casual = $"{n.Casual} {(f.HomeGoals > f.AwayGoals ? f.AwayTeam : f.HomeTeam)} are in front now." }),
        new("Invented history", "adds a historical claim with no number in it", "critic",
            (f, n) => n with { Casual = $"{n.Casual} It's the first time they have done that here in a decade." })
    ];

    public static async Task<RedTeamResult> RunAsync(EvalOptions options, VerifierCritic? critic)
    {
        var samples = SampleMoments(options.RedTeam);
        var results = new List<AttackResult>();
        using var throttle = new SemaphoreSlim(4);

        async Task<Review> Check(MomentFacts facts, Narrated lines)
        {
            if (critic is null)
                return Review.Skipped;
            await throttle.WaitAsync();
            try
            {
                return await critic.ReviewAsync(facts, lines, null, CancellationToken.None);
            }
            finally
            {
                throttle.Release();
            }
        }

        foreach (var attack in Attacks)
        {
            var cases = samples.Select(f => (Facts: f, Lines: attack.Apply(f, TemplateNarrator.Narrate(f))))
                .Where(c => c.Lines is not null).ToList();
            var outcomes = await Task.WhenAll(cases.Select(async c =>
            {
                var byCode = NarrationVerifier.UnsupportedNumbers(c.Lines!, c.Facts).Count > 0;
                var byCritic = !byCode && !(await Check(c.Facts, c.Lines!)).Pass; // the critic only sees what passes the code check
                return (byCode, byCritic);
            }));
            results.Add(new AttackResult(attack.Name, attack.Description, attack.ExpectedCatcher, cases.Count,
                outcomes.Count(o => o.byCode), outcomes.Count(o => o.byCritic), outcomes.Count(o => o.byCode || o.byCritic)));
            Console.WriteLine($"  {attack.Name}: {results[^1].Caught}/{cases.Count}");
        }

        var clean = critic is null ? [] : await Task.WhenAll(samples.Select(async f => (Facts: f, Review: await Check(f, TemplateNarrator.Narrate(f)))));
        var falseAlarms = clean.Where(c => !c.Review.Pass).SelectMany(c => c.Review.Issues.Select(i => $"{c.Facts.Moment.Type}: {i}")).ToList();
        return new RedTeamResult(samples.Count, results, clean.Length, clean.Count(c => !c.Review.Pass), critic is not null, falseAlarms);
    }

    /// <summary>Non-routine moments from a spread of matches, so every attack has something meaningful to change.</summary>
    private static List<MomentFacts> SampleMoments(int count)
    {
        var picked = new List<MomentFacts>();
        for (var seed = 500; picked.Count < count && seed < 600; seed++)
        {
            var match = new MatchEngine(seed, Clubs.KestrelBay, Clubs.RedmoorRovers).Play();
            var analyzer = new MatchAnalyzer(match.MatchId, match.Home.Id, match.Away.Id);
            var fromMatch = new List<MomentFacts>();
            foreach (var e in match.Events)
                foreach (var m in analyzer.Ingest(e).Where(m => m.Priority >= 45 && m.Type is not (MomentType.HalfTime or MomentType.FullTime)))
                    fromMatch.Add(MomentFacts.From(m, match.Info, analyzer.Snapshot()));
            picked.AddRange(fromMatch.Where((_, i) => i % 4 == 0).Take(3));
        }
        return picked.Take(count).ToList();
    }
}
