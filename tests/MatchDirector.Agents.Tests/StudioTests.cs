using MatchDirector.Agents.Narration;
using MatchDirector.Agents.Studio;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents.Tests;

public class StudioTests
{
    private static readonly MomentFacts Facts = new(
        new Moment("m-9:MomentumShift", MomentType.MomentumShift, TimeSpan.FromMinutes(70), 2, "RDM", null, 55,
            new Dictionary<string, double> { ["momentum"] = -48.3 }, ["m-5", "m-9"], []),
        71, "Redmoor Rovers", "Kestrel Bay FC", null, 1, 1, "Kestrel Bay FC", "Redmoor Rovers");

    [Fact]
    public void Insight_may_use_numbers_the_tools_returned_but_nothing_else()
    {
        var toolNumbers = NarrationVerifier.NumbersIn("""{"away":{"stats":{"xg":1.42,"pressures":61}}}""").ToList();
        var allowed = Facts.AllowedNumbers(toolNumbers);

        Assert.Empty(NarrationVerifier.UnsupportedNumbers(["Redmoor's 61 pressures and 1.42 xG explain a 48.3 swing."], allowed));
        Assert.Equal(["9"], NarrationVerifier.UnsupportedNumbers(["They have won 9 of their last games."], allowed));
    }

    [Fact]
    public void Tool_numbers_are_not_allowed_without_the_tool_call()
    {
        Assert.Equal(["61"], NarrationVerifier.UnsupportedNumbers(["61 pressures so far."], Facts.AllowedNumbers()));
    }

    [Theory]
    [InlineData("""{"pass": false, "issues": ["homeFan: written for the away team"]}""")]
    [InlineData("```json\n{\"pass\": false, \"issues\": [\"homeFan: written for the away team\"]}\n```")]
    [InlineData("""Here is my verdict: {"pass": false, "issues": ["homeFan: written for the away team"]} Thanks.""")]
    public void Agent_json_is_read_with_or_without_fences(string reply)
    {
        var verdict = AgentJson.Parse<Verdict>(reply);
        Assert.NotNull(verdict);
        Assert.False(verdict.Pass);
        Assert.Equal(["homeFan: written for the away team"], verdict.Issues);
    }

    [Fact]
    public void Unreadable_agent_replies_parse_as_null()
    {
        Assert.Null(AgentJson.Parse<Verdict>("I could not decide."));
        Assert.Null(AgentJson.Parse<Verdict>("{ not json }"));
    }

    private sealed record Verdict(bool Pass, List<string> Issues);
}
