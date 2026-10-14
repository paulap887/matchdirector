using MatchDirector.Agents.Narration;
using MatchDirector.Simulator.Engine;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents.Tests;

public class NarrationVerifierTests
{
    private static readonly MomentFacts Goal = new(
        new Moment("m-9:Goal", MomentType.Goal, TimeSpan.FromMinutes(63.4), 2, "KBY", "KBY-09", 100,
            new Dictionary<string, double> { ["xg"] = 0.345, ["distance"] = 14.2, ["shotSpeed"] = 23.8, ["passesInMove"] = 4 },
            ["m-5", "m-9"], ["KBY-09"]),
        64, "Kestrel Bay FC", "Redmoor Rovers", "Dario Okonkwe", 2, 1, "Kestrel Bay FC", "Redmoor Rovers");

    [Theory]
    [InlineData("Okonkwe scores in minute 64! 2-1.")]
    [InlineData("A 0.35 xG chance from 14.2 m, struck at 23.8 m/s.")]
    [InlineData("A 35% chance, finished after a 4-pass move from 14 metres.")]
    public void Numbers_from_the_facts_pass(string line)
    {
        Assert.Empty(NarrationVerifier.UnsupportedNumbers(Lines(line), Goal));
    }

    [Theory]
    [InlineData("Okonkwe's 12th goal of the season!", "12")]
    [InlineData("Struck at 31 m/s from 14.2 m.", "31")]
    [InlineData("A 0.5 xG chance.", "0.5")]
    public void Invented_numbers_are_rejected(string line, string invented)
    {
        Assert.Equal([invented], NarrationVerifier.UnsupportedNumbers(Lines(line), Goal));
    }

    [Fact]
    public void Every_line_is_checked_including_fan_lines()
    {
        var narration = new Narrated("Goal!", "Clinical finish.", "We lead 2-1!", "Down 1-3 now.", "agent");
        Assert.Equal(["3"], NarrationVerifier.UnsupportedNumbers(narration, Goal));
    }

    public static TheoryData<int> Seeds => new(Enumerable.Range(1, 10));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Template_narration_always_passes_the_verifier(int seed)
    {
        var match = new MatchEngine(seed, Clubs.KestrelBay, Clubs.RedmoorRovers).Play();
        var analyzer = new MatchAnalyzer(match.MatchId, match.Home.Id, match.Away.Id);

        foreach (var e in match.Events)
        {
            foreach (var moment in analyzer.Ingest(e))
            {
                var facts = MomentFacts.From(moment, match.Info, analyzer.Snapshot());
                var narration = TemplateNarrator.Narrate(facts);
                Assert.Empty(NarrationVerifier.UnsupportedNumbers(narration, facts));
                Assert.All(narration.Lines, line => Assert.False(string.IsNullOrWhiteSpace(line)));
            }
        }
    }

    [Fact]
    public void Fan_lines_take_sides()
    {
        var narration = TemplateNarrator.Narrate(Goal);
        Assert.StartsWith("YES!", narration.HomeFan);
        Assert.StartsWith("Gutted", narration.AwayFan);
        Assert.Contains("2-1", narration.HomeFan);
        Assert.Contains("1-2", narration.AwayFan);
    }

    private static Narrated Lines(string line) => new(line, line, line, line, "agent");
}
