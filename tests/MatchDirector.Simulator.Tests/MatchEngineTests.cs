using MatchDirector.Contracts;
using MatchDirector.Simulator.Engine;

namespace MatchDirector.Simulator.Tests;

public class MatchEngineTests
{
    private static SimulatedMatch Play(int seed) =>
        new MatchEngine(seed, Clubs.KestrelBay, Clubs.RedmoorRovers).Play();

    [Fact]
    public void Same_seed_produces_identical_match()
    {
        Assert.Equal(Play(42).Events, Play(42).Events);
    }

    [Fact]
    public void Different_seeds_produce_different_matches()
    {
        Assert.NotEqual(Play(1).Events, Play(2).Events);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(2026)]
    public void Events_are_ordered_unique_and_on_the_pitch(int seed)
    {
        var events = Play(seed).Events;

        Assert.Equal(events.Count, events.Select(e => e.EventId).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, events.Count), events.Select(e => e.Sequence));
        foreach (var period in new[] { 1, 2 })
        {
            var clocks = events.Where(e => e.Period == period).Select(e => e.MatchClock).ToList();
            Assert.Equal(clocks.Order(), clocks);
        }

        Assert.All(events, e =>
        {
            Assert.InRange(e.Start.X, 0, 105);
            Assert.InRange(e.Start.Y, 0, 68);
            if (e.End is { } end)
            {
                Assert.InRange(end.X, 0, 105);
                Assert.InRange(end.Y, 0, 68);
            }
        });
    }

    [Fact]
    public void Match_has_two_halves_of_regulation_length()
    {
        var events = Play(11).Events;

        Assert.Equal(MatchEventType.KickOff, events[0].Type);
        var ends = events.Where(e => e.Type == MatchEventType.PeriodEnd).ToList();
        Assert.Equal([1, 2], ends.Select(e => e.Period));
        Assert.InRange(ends[0].MatchClock.TotalMinutes, 45, 49);
        Assert.InRange(ends[1].MatchClock.TotalMinutes, 90, 97);
        Assert.Equal(MatchEventType.PeriodEnd, events[^1].Type);
    }

    [Fact]
    public void Every_goal_follows_a_successful_shot_and_is_followed_by_a_kick_off_for_the_other_team()
    {
        var events = Play(5).Events;

        for (var i = 0; i < events.Count; i++)
        {
            if (events[i].Type != MatchEventType.Goal)
                continue;

            var shot = events[i - 1];
            Assert.Equal(MatchEventType.Shot, shot.Type);
            Assert.Equal(EventOutcome.Success, shot.Outcome);
            Assert.Equal(shot.PlayerId, events[i].PlayerId);

            var restart = events.Skip(i + 1).First(e => e.Type is MatchEventType.KickOff or MatchEventType.PeriodEnd);
            if (restart.Type == MatchEventType.KickOff)
                Assert.NotEqual(events[i].TeamId, restart.TeamId);
        }
    }

    [Fact]
    public void Possession_changes_always_switch_team()
    {
        var events = Play(3).Events;
        string? holder = null;

        foreach (var e in events)
        {
            if (e.Type == MatchEventType.PossessionChange)
                Assert.NotEqual(holder, e.TeamId);
            if (e.Type is MatchEventType.KickOff or MatchEventType.PossessionChange)
                holder = e.TeamId;
        }
    }

    [Fact]
    public void Ball_travel_has_a_plausible_speed()
    {
        var moves = Play(9).Events.Where(e => e.End is not null && e.Duration is not null).ToList();

        Assert.NotEmpty(moves);
        Assert.All(moves, e =>
        {
            var dx = e.End!.Value.X - e.Start.X;
            var dy = e.End!.Value.Y - e.Start.Y;
            var speed = Math.Sqrt(dx * dx + dy * dy) / e.Duration!.Value.TotalSeconds;
            Assert.InRange(speed, 0, 40);
        });
    }

    [Fact]
    public void Season_of_matches_has_realistic_totals()
    {
        var matches = Enumerable.Range(1, 60).Select(Play).ToList();

        double PerMatch(Func<MatchEvent, bool> predicate) => matches.Average(m => m.Events.Count(predicate));
        var goals = PerMatch(e => e.Type == MatchEventType.Goal);
        var shots = PerMatch(e => e.Type == MatchEventType.Shot);
        var passes = matches.SelectMany(m => m.Events).Where(e => e.Type == MatchEventType.Pass).ToList();
        var accuracy = passes.Count(p => p.Outcome == EventOutcome.Success) / (double)passes.Count;

        Assert.InRange(goals, 2.0, 3.6);
        Assert.InRange(shots, 18, 32);
        Assert.InRange(passes.Count / 60.0, 700, 1200);
        Assert.InRange(accuracy, 0.75, 0.88);
    }
}
