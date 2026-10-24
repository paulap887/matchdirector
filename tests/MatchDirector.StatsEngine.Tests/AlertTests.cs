using MatchDirector.Contracts;

namespace MatchDirector.StatsEngine.Tests;

public class AlertTests
{
    private const string Home = "HOME", Away = "AWAY";

    private static MatchEvent Event(int sequence, double seconds, MatchEventType type, string team, string? player,
        PitchPoint start, PitchPoint? end = null, EventOutcome outcome = EventOutcome.None, double? duration = null) =>
        new($"m-{sequence}", "m", sequence, TimeSpan.FromSeconds(seconds), 1, type, team, player, start, end, null, outcome,
            Duration: duration is { } d ? TimeSpan.FromSeconds(d) : null);

    private static List<Moment> Run(params MatchEvent[] events)
    {
        var analyzer = new MatchAnalyzer("m", Home, Away);
        return events.SelectMany(analyzer.Ingest).ToList();
    }

    [Fact]
    public void A_fast_shot_with_no_other_moment_raises_a_speed_alert()
    {
        // 31 m from goal (low xG), struck at about 32 m/s and missed: not a big chance, but over the speed threshold.
        var moments = Run(
            Event(1, 0, MatchEventType.KickOff, Home, "HOME-09", new(52.5, 34)),
            Event(2, 5, MatchEventType.Shot, Home, "HOME-10", new(74, 34), new(105, 40), EventOutcome.Failure, duration: 1.0),
            Event(3, 30, MatchEventType.PossessionChange, Away, "AWAY-01", new(99, 34)));

        var rocket = Assert.Single(moments, m => m.Type == MomentType.RocketShot);
        Assert.Equal(Thresholds.RocketShotSpeed, rocket.Metrics["speedThreshold"]);
        Assert.True(rocket.Metrics["shotSpeed"] >= Thresholds.RocketShotSpeed);
        Assert.Equal(["m-2"], rocket.Evidence);
    }

    [Fact]
    public void A_fast_goal_carries_the_alert_instead_of_a_second_moment()
    {
        var moments = Run(
            Event(1, 0, MatchEventType.KickOff, Home, "HOME-09", new(52.5, 34)),
            Event(2, 5, MatchEventType.Shot, Home, "HOME-09", new(92, 34), new(105, 35), EventOutcome.Success, duration: 0.4),
            Event(3, 6, MatchEventType.Goal, Home, "HOME-09", new(105, 35), outcome: EventOutcome.Success));

        var goal = Assert.Single(moments);
        Assert.Equal(MomentType.Goal, goal.Type);
        Assert.Equal(Thresholds.RocketShotSpeed, goal.Metrics["speedThreshold"]);
        Assert.Equal(1, goal.Metrics["playerGoals"]);
    }

    [Fact]
    public void A_second_goal_by_the_same_player_is_marked_as_a_brace()
    {
        var moments = Run(
            Event(1, 0, MatchEventType.KickOff, Home, "HOME-09", new(52.5, 34)),
            Event(2, 5, MatchEventType.Shot, Home, "HOME-09", new(92, 34), new(105, 35), EventOutcome.Success, duration: 1.0),
            Event(3, 6, MatchEventType.Goal, Home, "HOME-09", new(105, 35), outcome: EventOutcome.Success),
            Event(4, 70, MatchEventType.KickOff, Away, "AWAY-09", new(52.5, 34)),
            Event(5, 80, MatchEventType.PossessionChange, Home, "HOME-09", new(60, 34)),
            Event(6, 85, MatchEventType.Shot, Home, "HOME-09", new(94, 34), new(105, 33), EventOutcome.Success, duration: 1.0),
            Event(7, 86, MatchEventType.Goal, Home, "HOME-09", new(105, 33), outcome: EventOutcome.Success));

        Assert.Equal([1, 2], moments.Where(m => m.Type == MomentType.Goal).Select(m => m.Metrics["playerGoals"]));
    }

    [Fact]
    public void A_fast_long_carry_is_a_driving_run()
    {
        var moments = Run(
            Event(1, 0, MatchEventType.KickOff, Home, "HOME-09", new(52.5, 34)),
            Event(2, 3, MatchEventType.Carry, Home, "HOME-07", new(55, 34), new(70, 34), EventOutcome.Success, duration: 2.0));

        var run = Assert.Single(moments, m => m.Type == MomentType.DrivingRun);
        Assert.Equal(7.5, run.Metrics["speed"]);
        Assert.Equal(15, run.Metrics["distance"]);
        Assert.Equal(Thresholds.DrivingRunSpeed, run.Metrics["speedThreshold"]);
    }

    [Fact]
    public void A_slow_or_short_carry_is_not()
    {
        var moments = Run(
            Event(1, 0, MatchEventType.KickOff, Home, "HOME-09", new(52.5, 34)),
            Event(2, 3, MatchEventType.Carry, Home, "HOME-07", new(55, 34), new(70, 34), EventOutcome.Success, duration: 3.0),  // 5 m/s
            Event(3, 9, MatchEventType.Carry, Home, "HOME-07", new(70, 34), new(78, 34), EventOutcome.Success, duration: 1.0)); // 8 m only

        Assert.DoesNotContain(moments, m => m.Type == MomentType.DrivingRun);
    }

    [Fact]
    public void Each_150_metres_carried_is_announced_once()
    {
        var events = new List<MatchEvent> { Event(1, 0, MatchEventType.KickOff, Home, "HOME-09", new(52.5, 34)) };
        for (var i = 0; i < 25; i++) // 25 carries of 13 m, slowly, so only the distance counts
            events.Add(Event(i + 2, 10 + i * 10, MatchEventType.Carry, Home, "HOME-07", new(40, 34), new(53, 34), EventOutcome.Success, duration: 4.0));

        var milestones = Run([.. events]).Where(m => m.Type == MomentType.DistanceMilestone).ToList();
        Assert.Equal([150, 300], milestones.Select(m => m.Metrics["metresCarried"]));
        Assert.All(milestones, m => Assert.Equal("HOME-07", m.PlayerId));
    }
}
