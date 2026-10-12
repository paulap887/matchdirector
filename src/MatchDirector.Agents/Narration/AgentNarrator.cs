using System.Text.Json;
using MatchDirector.Contracts;
using MatchDirector.StatsEngine;
using Microsoft.Agents.AI;

namespace MatchDirector.Agents.Narration;

/// <summary>
/// Narrator agent (Microsoft Agent Framework) on the fast model. It only words the facts it is given;
/// <see cref="NarrationVerifier"/> checks every number before anything reaches the screen.
/// </summary>
public sealed class AgentNarrator(AIAgent agent, ILogger<AgentNarrator> logger) : INarrator
{
    public const string Instructions = """
        You are the live narrator for a football broadcast overlay. You receive one moment as JSON facts.
        Write two lines about it:
        - "casual": for a casual fan. Warm, plain words, at most 14 words, no jargon, at most one number.
        - "analyst": for an analyst. Precise, at most 22 words, uses the metrics.
        Rules: use ONLY the facts given. Never invent players, events or numbers. Every number you write must
        appear in the facts exactly (you may write a 0-1 metric as a percentage). Always use the unit given in "units"
        for a metric and never convert it. Use player and team names as given.
        Reply with JSON only: {"casual": "...", "analyst": "..."}
        """;

    public async Task<Narrated> NarrateAsync(MomentFacts facts, CancellationToken cancellationToken)
    {
        var prompt = JsonSerializer.Serialize(new
        {
            moment = facts.Moment.Type.ToString(),
            meaning = Meaning(facts),
            minute = facts.Minute,
            team = facts.Team,
            opponent = facts.Opponent,
            player = facts.Player,
            score = new { home = facts.HomeTeam, homeGoals = facts.HomeGoals, away = facts.AwayTeam, awayGoals = facts.AwayGoals },
            metrics = facts.Moment.Metrics,
            units = facts.Moment.Metrics.Keys.ToDictionary(k => k, Unit)
        }, MatchDirectorJson.Options);

        try
        {
            var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken);
            var text = response.Text.Trim().Trim('`');
            if (text.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                text = text[4..];
            var lines = JsonSerializer.Deserialize<Lines>(text, MatchDirectorJson.Options);
            if (lines is { Casual.Length: > 0, Analyst.Length: > 0 })
                return new Narrated(lines.Casual, lines.Analyst, "agent");
            logger.LogWarning("Narrator returned unusable output for {MomentId}", facts.Moment.MomentId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Narrator failed for {MomentId}", facts.Moment.MomentId);
        }
        return TemplateNarrator.Narrate(facts);
    }

    private static string Unit(string metric) => metric switch
    {
        "distance" or "distanceToGoal" or "progression" => "metres",
        "speed" or "shotSpeed" => "metres per second",
        "seconds" or "moveSeconds" => "seconds",
        "angle" => "degrees",
        "xg" or "homeXg" or "awayXg" => "expected goals (0-1 probability for a single shot)",
        "difficulty" => "0-1 scale",
        "possession" or "homePossession" or "awayPossession" or "homePassAccuracy" or "awayPassAccuracy" => "percent",
        "momentum" => "-100 to 100 (positive = home on top)",
        "chaos" => "0-100 (higher = more chaotic)",
        _ => "count"
    };

    /// <summary>What the moment means in plain words, so the model does not have to guess how to read a metric.</summary>
    private static string Meaning(MomentFacts facts) => facts.Moment.Type switch
    {
        MomentType.Goal => "the team scored; score already includes this goal",
        MomentType.BigChanceMissed => "a high-quality chance (xg = probability of scoring) was not scored",
        MomentType.KeySave => "the named goalkeeper saved a dangerous shot against their team",
        MomentType.PassingMove => "the team strung together a long run of completed passes",
        MomentType.DifficultPass => "the named player completed a hard pass (difficulty 0-1) that moved the ball towards goal",
        MomentType.HighRegain => "the team won the ball back close to the opponent's goal (pressing)",
        MomentType.MomentumShift => "the team has created most of the recent threat (momentum -100..100, positive = home)",
        MomentType.TempoShift => facts.Moment.Metrics.GetValueOrDefault("chaos") >= 50
            ? "the game has become chaotic and end-to-end: many turnovers, short possessions (chaos 0-100)"
            : "the game has calmed down into controlled possession (chaos 0-100)",
        MomentType.HalfTime => "half-time summary",
        MomentType.FullTime => "full-time summary",
        _ => facts.Moment.Type.ToString()
    };

    private sealed record Lines(string Casual, string Analyst);
}
