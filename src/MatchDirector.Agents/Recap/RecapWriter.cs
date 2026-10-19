using Microsoft.Agents.AI;
using MatchDirector.Agents.Studio;

namespace MatchDirector.Agents.Recap;

/// <summary>Recap writer agent on the reasoning model: turns the full-time facts into a report for each audience.</summary>
public sealed class RecapWriter(AIAgent agent, ILogger<RecapWriter> logger)
{
    public const string Instructions = """
        You write the full-time match report for a football broadcast app. You receive the match FACTS as JSON, all
        computed by code: final team stats, the player of the match and the key moments.
        Write:
        - "headline": at most 10 words.
        - "casual": for a casual fan, 2-3 short sentences, at most 60 words, plain words, at most two numbers.
        - "analyst": for an analyst, 3-4 sentences, at most 90 words, explaining how the result happened with the stats.
        - "homeFan": a supporter of the club in fans.homeFan ("we"/"us" = that club), 2 sentences, at most 45 words.
        - "awayFan": the same for a supporter of the club in fans.awayFan.
        Mention the player of the match by name in casual and analyst. Use ONLY the facts: never invent players,
        events, history or numbers, and write every number exactly as it appears in the facts (keep units).
        Name stats in plain words ("shots on target", "pass accuracy"), never as JSON field names.
        Reply with JSON only: {"headline": "...", "casual": "...", "analyst": "...", "homeFan": "...", "awayFan": "..."}
        """;

    public async Task<RecapLines?> WriteAsync(RecapFacts facts, CancellationToken ct)
    {
        try
        {
            var response = await agent.RunAsync(facts.ToJson(), cancellationToken: ct);
            if (AgentJson.Parse<Reply>(response.Text) is { Headline.Length: > 0, Casual.Length: > 0, Analyst.Length: > 0, HomeFan.Length: > 0, AwayFan.Length: > 0 } r)
                return new RecapLines(r.Headline, r.Casual, r.Analyst, r.HomeFan, r.AwayFan, "agent");
            logger.LogWarning("Recap writer returned unusable output for {MatchId}", facts.Info.MatchId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Recap writer failed for {MatchId}", facts.Info.MatchId);
        }
        return null;
    }

    private sealed record Reply(string Headline, string Casual, string Analyst, string HomeFan, string AwayFan);
}
