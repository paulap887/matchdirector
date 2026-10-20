using System.Text.Json;
using MatchDirector.Agents.Narration;
using MatchDirector.Contracts;
using Microsoft.Agents.AI;

namespace MatchDirector.Agents.Studio;

public sealed record Review(bool Pass, IReadOnlyList<string> Issues)
{
    public static readonly Review Skipped = new(true, []);
}

/// <summary>
/// Fact-checking agent on the reasoning model. Code already guarantees every number is real; this agent checks
/// meaning: right team and player, right side for fan lines, right score story, and no invented claims.
/// </summary>
public sealed class VerifierCritic(AIAgent agent, ILogger<VerifierCritic> logger)
{
    public const string Instructions = """
        You are the fact-checker in a live football broadcast studio. You receive FACTS, computed by code, about one
        moment or about a whole match at full time, and the LINES writers produced for different audiences.
        Numbers have already been checked by code.
        Flag only real problems:
        - a claim about the wrong team or player, or a player not in the facts;
        - a wrong score story (who leads, who equalised, who is behind);
        - homeFan not written as a supporter of the home club (score.home), or awayFan not as the away club's
          (a fan may praise the other club's play, but "we/us" must always mean their own club);
        - events, history, records, totals, streaks or causes that the facts do not support;
        - a metric described with the wrong meaning (see "meaning" and "units").
        The "insight" line comes from the tactical analyst, who looked up match stats with code tools; judge it
        against both the facts and "analystEvidence" (the exact tool results). Claims found there are supported.
        Do not flag style, tone, harmless paraphrase or a number's rounding.
        Reply with JSON only: {"pass": true|false, "issues": ["<line name>: <problem>"]}. Use line names
        headline, casual, analyst, homeFan, awayFan or insight.
        """;

    public async Task<Review> ReviewAsync(MomentFacts facts, Narrated lines, Insight? insight, CancellationToken ct)
    {
        var prompt = JsonSerializer.Serialize(new
        {
            facts = new
            {
                moment = facts.Moment.Type.ToString(),
                meaning = AgentNarrator.Meaning(facts),
                minute = facts.Minute,
                team = facts.Team,
                opponent = facts.Opponent,
                player = facts.Player,
                playerRole = facts.PlayerRole,
                score = new { home = facts.HomeTeam, homeGoals = facts.HomeGoals, away = facts.AwayTeam, awayGoals = facts.AwayGoals },
                fans = new { homeFan = facts.FanViewFor(home: true), awayFan = facts.FanViewFor(home: false) },
                metrics = facts.Moment.Metrics,
                units = facts.Moment.Metrics.Keys.ToDictionary(k => k, AgentNarrator.Unit)
            },
            lines = new { lines.Casual, lines.Analyst, lines.HomeFan, lines.AwayFan, insight = insight?.Text },
            analystEvidence = insight?.ToolResults.Select(r => new { r.Tool, result = JsonDocument.Parse(r.Json).RootElement })
        }, MatchDirectorJson.Options);

        try
        {
            var response = await agent.RunAsync(prompt, cancellationToken: ct);
            if (AgentJson.Parse<Verdict>(response.Text) is { } verdict)
                return new Review(verdict.Pass && verdict.Issues is not { Count: > 0 }, verdict.Issues ?? []);
            logger.LogWarning("Verifier returned unusable output for {MomentId}", facts.Moment.MomentId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Verifier failed for {MomentId}", facts.Moment.MomentId);
        }
        return Review.Skipped; // the code number check has already run; never block the broadcast on the reviewer
    }

    /// <summary>Fact-checks a full-time report against the match facts.</summary>
    public async Task<Review> ReviewRecapAsync(Recap.RecapFacts facts, Recap.RecapLines lines, CancellationToken ct)
    {
        var prompt = $$"""
            {"facts": {{facts.ToJson()}},
             "lines": {{JsonSerializer.Serialize(new { lines.Headline, lines.Casual, lines.Analyst, lines.HomeFan, lines.AwayFan }, MatchDirectorJson.Options)}}}
            """;
        try
        {
            var response = await agent.RunAsync(prompt, cancellationToken: ct);
            if (AgentJson.Parse<Verdict>(response.Text) is { } verdict)
                return new Review(verdict.Pass && verdict.Issues is not { Count: > 0 }, verdict.Issues ?? []);
            logger.LogWarning("Verifier returned unusable output for the {MatchId} recap", facts.Info.MatchId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Verifier failed for the {MatchId} recap", facts.Info.MatchId);
        }
        return Review.Skipped;
    }

    private sealed record Verdict(bool Pass, List<string>? Issues);
}
