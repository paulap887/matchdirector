using MatchDirector.Contracts;
using MatchDirector.Simulator.Engine;

namespace MatchDirector.StatsEngine.Tests;

public class MatchAnalyzerTests
{
    private const string Home = "HOME", Away = "AWAY";

    [Fact]
    public void Goal_moment_cites_the_build_up_shot_and_goal()
    {
        var events = new List<MatchEvent>
        {
            Event(1, 0, MatchEventType.KickOff, Home, "HOME-09", new(52.5, 34)),
            Event(2, 3, MatchEventType.Pass, Home, "HOME-09", new(52.5, 34), new(70, 40), EventOutcome.Success),
            Event(3, 6, MatchEventType.Pass, Home, "HOME-08", new(70, 40), new(92, 36), EventOutcome.Success),
            Event(4, 8, MatchEventType.Shot, Home, "HOME-10", new(92, 36), new(105, 35), EventOutcome.Success),
            Event(5, 9, MatchEventType.Goal, Home, "HOME-10", new(105, 35), outcome: EventOutcome.Success)
        };

        var analyzer = new MatchAnalyzer("m", Home, Away);
        var moments = events.SelectMany(analyzer.Ingest).ToList();

        var goal = Assert.Single(moments, m => m.Type == MomentType.Goal);
        Assert.Equal("HOME-10", goal.PlayerId);
        Assert.Equal(["m-1", "m-2", "m-3", "m-4", "m-5"], goal.Evidence);
        Assert.Equal(2, goal.Metrics["passesInMove"]);
        Assert.Equal(1, goal.Metrics["homeScore"]);
        Assert.Equal(0, goal.Metrics["awayScore"]);

        var snapshot = analyzer.Snapshot();
        Assert.Equal(1, snapshot.Home.Goals);
        Assert.Equal(1, snapshot.Home.ShotsOnTarget);
        Assert.Equal(100, snapshot.Home.PassAccuracy);
    }

    [Fact]
    public void A_missed_big_chance_is_flagged_and_a_saved_long_shot_is_not()
    {
        var events = new List<MatchEvent>
        {
            Event(1, 0, MatchEventType.KickOff, Home, "HOME-09", new(52.5, 34)),
            Event(2, 5, MatchEventType.Shot, Home, "HOME-09", new(99, 34), new(105, 45), EventOutcome.Failure),
            Event(3, 30, MatchEventType.PossessionChange, Away, "AWAY-01", new(99, 34)),
            Event(4, 60, MatchEventType.PossessionChange, Home, "HOME-04", new(60, 34)),
            Event(5, 65, MatchEventType.Shot, Home, "HOME-04", new(75, 34), new(104, 33), EventOutcome.Failure),
            Event(6, 66, MatchEventType.Save, Away, "AWAY-01", new(104, 33), outcome: EventOutcome.Success)
        };

        var analyzer = new MatchAnalyzer("m", Home, Away);
        var moments = events.SelectMany(analyzer.Ingest).ToList();

        var miss = Assert.Single(moments);
        Assert.Equal(MomentType.BigChanceMissed, miss.Type);
        Assert.Equal(["m-2"], miss.Evidence);
        Assert.Equal(1, analyzer.Snapshot().Home.ShotsOnTarget);
    }

    [Fact]
    public void Possession_is_split_by_time_on_the_ball()
    {
        var analyzer = new MatchAnalyzer("m", Home, Away);
        analyzer.Ingest(Event(1, 0, MatchEventType.KickOff, Home, "HOME-09", new(52.5, 34)));
        analyzer.Ingest(Event(2, 30, MatchEventType.PossessionChange, Away, "AWAY-06", new(52.5, 34)));
        analyzer.Ingest(Event(3, 40, MatchEventType.PossessionChange, Home, "HOME-06", new(52.5, 34)));

        var snapshot = analyzer.Snapshot();
        Assert.Equal(75, snapshot.Home.Possession);
        Assert.Equal(25, snapshot.Away.Possession);
    }

    public static TheoryData<int> Seeds => new(Enumerable.Range(1, 20));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Every_moment_cites_real_earlier_events(int seed)
    {
        var (match, moments) = Analyse(seed);
        var sequenceById = match.Events.ToDictionary(e => e.EventId, e => e.Sequence);

        Assert.All(moments, m =>
        {
            Assert.NotEmpty(m.Evidence);
            Assert.All(m.Evidence, id => Assert.True(sequenceById.ContainsKey(id), $"{m.MomentId} cites unknown event {id}"));
            Assert.All(m.Evidence, id => Assert.True(sequenceById[id] <= m.TriggeredBy, $"{m.MomentId} cites future event {id}"));
            Assert.All(m.Metrics.Values, v => Assert.True(double.IsFinite(v)));
        });
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Score_and_goal_moments_match_the_event_stream(int seed)
    {
        var (match, moments) = Analyse(seed);
        var analyzer = Replay(match);
        var snapshot = analyzer.Snapshot();
        var goals = match.Events.Where(e => e.Type == MatchEventType.Goal).ToList();

        Assert.Equal(goals.Count(g => g.TeamId == match.Home.Id), snapshot.Home.Goals);
        Assert.Equal(goals.Count(g => g.TeamId == match.Away.Id), snapshot.Away.Goals);
        Assert.Equal(goals.Select(g => g.EventId), moments.Where(m => m.Type == MomentType.Goal).Select(m => m.Evidence[^1]));
        Assert.Equal(100, snapshot.Home.Possession + snapshot.Away.Possession, 0.2);
        Assert.Single(moments, m => m.Type == MomentType.HalfTime);
        Assert.Single(moments, m => m.Type == MomentType.FullTime);
    }

    [Fact]
    public void A_match_produces_a_watchable_number_of_moments()
    {
        var perMatch = Enumerable.Range(1, 30).Select(s => Analyse(s).Moments).ToList();
        double Average(MomentType type) => perMatch.Average(m => m.Count(x => x.Type == type));

        Assert.InRange(perMatch.Average(m => m.Count), 15, 40);
        foreach (var type in new[] { MomentType.PassingMove, MomentType.DifficultPass, MomentType.HighRegain, MomentType.MomentumShift, MomentType.TempoShift })
            Assert.InRange(Average(type), 0.5, 12);
    }

    private static (SimulatedMatch Match, List<TriggeredMoment> Moments) Analyse(int seed)
    {
        var match = new MatchEngine(seed, Clubs.KestrelBay, Clubs.RedmoorRovers).Play();
        var analyzer = new MatchAnalyzer(match.MatchId, match.Home.Id, match.Away.Id);
        var moments = match.Events
            .SelectMany(e => analyzer.Ingest(e).Select(m => new TriggeredMoment(m, e.Sequence)))
            .ToList();
        return (match, moments);
    }

    private static MatchAnalyzer Replay(SimulatedMatch match)
    {
        var analyzer = new MatchAnalyzer(match.MatchId, match.Home.Id, match.Away.Id);
        foreach (var e in match.Events)
            analyzer.Ingest(e);
        return analyzer;
    }

    private sealed record TriggeredMoment(Moment Moment, int TriggeredBy)
    {
        public MomentType Type => Moment.Type;
        public string MomentId => Moment.MomentId;
        public IReadOnlyList<string> Evidence => Moment.Evidence;
        public IReadOnlyDictionary<string, double> Metrics => Moment.Metrics;
    }

    private static MatchEvent Event(int sequence, double seconds, MatchEventType type, string team, string? player,
        PitchPoint start, PitchPoint? end = null, EventOutcome outcome = EventOutcome.None) =>
        new($"m-{sequence}", "m", sequence, TimeSpan.FromSeconds(seconds), 1, type, team, player, start, end, null, outcome);
}
