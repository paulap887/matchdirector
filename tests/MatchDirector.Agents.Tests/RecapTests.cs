using MatchDirector.Agents.Narration;
using MatchDirector.Agents.Recap;
using MatchDirector.Agents.Studio;
using MatchDirector.Simulator.Engine;

namespace MatchDirector.Agents.Tests;

public class RecapTests
{
    private static RecapFacts FullTime(int seed)
    {
        var match = new MatchEngine(seed, Clubs.KestrelBay, Clubs.RedmoorRovers).Play();
        var session = new MatchSession(match.Info);
        foreach (var e in match.Events)
            session.Ingest(e);
        return session.RecapFacts();
    }

    public static TheoryData<int> Seeds => new(Enumerable.Range(1, 15));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Template_recap_passes_the_number_check(int seed)
    {
        var facts = FullTime(seed);
        var recap = TemplateRecap.Write(facts);
        Assert.Empty(NarrationVerifier.UnsupportedNumbers(recap.All, facts.AllowedNumbers()));
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Recap_names_the_player_of_the_match_and_takes_the_right_side(int seed)
    {
        var facts = FullTime(seed);
        var recap = TemplateRecap.Write(facts);

        Assert.Contains(facts.Name(facts.Best!.PlayerId)!, recap.Casual);
        Assert.Contains($"{facts.HomeGoals}-{facts.AwayGoals}", recap.Casual);
        if (facts.HomeGoals > facts.AwayGoals)
        {
            Assert.StartsWith("We win", recap.HomeFan);
            Assert.StartsWith("Beaten", recap.AwayFan);
        }
        else if (facts.HomeGoals < facts.AwayGoals)
        {
            Assert.StartsWith("Beaten", recap.HomeFan);
            Assert.StartsWith("We win", recap.AwayFan);
        }
    }

    [Fact]
    public void Key_moments_keep_every_goal_and_cite_their_events()
    {
        var facts = FullTime(5);
        Assert.Equal(facts.HomeGoals + facts.AwayGoals, facts.KeyMoments.Count(m => m.Type == "Goal"));
        Assert.All(facts.KeyMoments, m => Assert.NotEmpty(m.Evidence));
        Assert.Equal(facts.KeyMoments.Select(m => m.Minute).Order(), facts.KeyMoments.Select(m => m.Minute));
    }

    [Fact]
    public void Invented_recap_numbers_are_rejected_and_counts_are_not_stretched()
    {
        var facts = FullTime(5);
        var shots = facts.Final.Home.Shots;
        var line = $"{shots} shots, {shots * 100}% effort and a club-record 987th win.";
        var unsupported = NarrationVerifier.UnsupportedNumbers([line], facts.AllowedNumbers());
        Assert.Contains($"{shots * 100}", unsupported);
        Assert.Contains("987", unsupported);
        Assert.DoesNotContain($"{shots}", unsupported);
    }
}
