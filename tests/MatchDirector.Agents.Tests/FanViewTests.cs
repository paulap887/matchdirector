using MatchDirector.Agents.Narration;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents.Tests;

public class FanViewTests
{
    private static MomentFacts Facts(MomentType type, string team, int homeGoals = 1, int awayGoals = 0) => new(
        new Moment($"m-1:{type}", type, TimeSpan.FromMinutes(30), 1, team == "Kestrel Bay FC" ? "KBY" : "RDM", null, 50,
            new Dictionary<string, double>(), ["m-1"], []),
        31, team, team == "Kestrel Bay FC" ? "Redmoor Rovers" : "Kestrel Bay FC", null, homeGoals, awayGoals, "Kestrel Bay FC", "Redmoor Rovers");

    [Theory]
    [InlineData(MomentType.Goal, "Kestrel Bay FC", "celebrate", "worried")]
    [InlineData(MomentType.KeySave, "Redmoor Rovers", "worried", "celebrate")]
    [InlineData(MomentType.BigChanceMissed, "Kestrel Bay FC", "frustrated", "relieved")]
    [InlineData(MomentType.BigChanceMissed, "Redmoor Rovers", "relieved", "frustrated")]
    [InlineData(MomentType.HighRegain, "Redmoor Rovers", "worried", "celebrate")]
    [InlineData(MomentType.TempoShift, "Kestrel Bay FC", "neutral", "neutral")]
    [InlineData(MomentType.RocketShot, "Kestrel Bay FC", "celebrate", "relieved")]
    [InlineData(MomentType.DrivingRun, "Redmoor Rovers", "worried", "celebrate")]
    public void Each_fan_gets_the_mood_of_their_own_side(MomentType type, string team, string homeMood, string awayMood)
    {
        var facts = Facts(type, team);
        Assert.Equal(homeMood, facts.FanViewFor(home: true).Mood);
        Assert.Equal(awayMood, facts.FanViewFor(home: false).Mood);
        Assert.Equal("Kestrel Bay FC", facts.FanViewFor(home: true).Club);
        Assert.Equal("Redmoor Rovers", facts.FanViewFor(home: false).Club);
    }

    [Theory]
    [InlineData(2, 1, "celebrate", "frustrated")]
    [InlineData(0, 3, "frustrated", "celebrate")]
    [InlineData(1, 1, "neutral", "neutral")]
    public void Full_time_mood_follows_the_score(int home, int away, string homeMood, string awayMood)
    {
        var facts = Facts(MomentType.FullTime, "Kestrel Bay FC", home, away);
        Assert.Equal(homeMood, facts.FanViewFor(home: true).Mood);
        Assert.Equal(awayMood, facts.FanViewFor(home: false).Mood);
        Assert.Equal("the whole match", facts.FanViewFor(home: true).Moment);
    }

    [Fact]
    public void The_named_player_in_a_save_is_the_goalkeeper()
    {
        var save = Facts(MomentType.KeySave, "Redmoor Rovers") with { Player = "Piotr Halvik" };
        Assert.Equal("goalkeeper who made the save (not the shooter)", save.PlayerRole);
        Assert.Null(Facts(MomentType.KeySave, "Redmoor Rovers").PlayerRole); // no player named, no role
    }
}
