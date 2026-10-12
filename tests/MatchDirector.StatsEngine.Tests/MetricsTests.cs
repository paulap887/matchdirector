using MatchDirector.Contracts;

namespace MatchDirector.StatsEngine.Tests;

public class MetricsTests
{
    [Fact]
    public void Xg_falls_with_distance_and_rises_with_a_central_angle()
    {
        var sixYards = ExpectedGoals.For(new PitchPoint(99.5, 34), false);
        var penaltySpot = ExpectedGoals.For(new PitchPoint(94, 34), false);
        var edgeOfBox = ExpectedGoals.For(new PitchPoint(86, 34), false);
        var tightAngle = ExpectedGoals.For(new PitchPoint(100, 10), false);

        Assert.True(sixYards > penaltySpot && penaltySpot > edgeOfBox);
        Assert.InRange(penaltySpot, 0.15, 0.35);
        Assert.True(tightAngle < penaltySpot);
    }

    [Fact]
    public void Pressure_lowers_xg()
    {
        var at = new PitchPoint(94, 34);
        Assert.True(ExpectedGoals.For(at, true) < ExpectedGoals.For(at, false));
    }

    [Theory]
    [InlineData(true, 1, 10, 10)]
    [InlineData(false, 1, 95, 58)]
    [InlineData(true, 2, 95, 58)]
    [InlineData(false, 2, 10, 10)]
    public void Attacking_frame_follows_the_direction_convention(bool isHome, int period, double expectedX, double expectedY)
    {
        var frame = Pitch.ToAttackingFrame(new PitchPoint(10, 10), isHome, period);
        Assert.Equal(new PitchPoint(expectedX, expectedY), frame);
    }

    [Fact]
    public void A_long_pass_into_the_box_under_pressure_is_progressive_and_difficult()
    {
        var pass = Pass(new PitchPoint(60, 20), new PitchPoint(96, 36), pressured: true, seconds: 1.8);
        var metrics = PassMetrics.From(pass, pass.Start, pass.End!.Value);

        Assert.True(metrics.Progressive);
        Assert.True(metrics.IntoBox);
        Assert.InRange(metrics.Difficulty, 0.75, 1);
        Assert.Equal(21.9, metrics.Speed);
    }

    [Fact]
    public void A_short_square_pass_is_easy_and_not_progressive()
    {
        var pass = Pass(new PitchPoint(40, 30), new PitchPoint(42, 40), pressured: false, seconds: 0.8);
        var metrics = PassMetrics.From(pass, pass.Start, pass.End!.Value);

        Assert.False(metrics.Progressive);
        Assert.InRange(metrics.Difficulty, 0, 0.2);
    }

    private static MatchEvent Pass(PitchPoint from, PitchPoint to, bool pressured, double seconds) =>
        new("m-1", "m", 1, TimeSpan.Zero, 1, MatchEventType.Pass, "HOME", "HOME-08", from, to, "HOME-09",
            EventOutcome.Success, pressured, TimeSpan.FromSeconds(seconds));
}
