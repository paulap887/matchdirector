using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using MatchDirector.Agents.Narration;
using MatchDirector.Contracts;
using MatchDirector.StatsEngine;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace MatchDirector.Agents.Studio;

/// <summary>What the analyst can look up: the match so far, as computed by the stats engine.</summary>
public sealed record StudioContext(MatchInfo Info, MatchSnapshot Snapshot, IReadOnlyList<RecentMoment> Recent);

public sealed record RecentMoment(int Minute, string Type, string Team, string? Player, IReadOnlyDictionary<string, double> Metrics);

/// <param name="ToolResults">What each tool returned, so the verifier can check the insight against what the analyst saw.</param>
public sealed record Insight(string Text, IReadOnlyList<ToolResult> ToolResults)
{
    public IReadOnlyList<string> ToolsUsed => ToolResults.Select(r => r.Tool).Distinct().ToList();

    /// <summary>Every number the tools returned; the insight may use these as well as the moment's facts.</summary>
    public IEnumerable<double> ToolNumbers => ToolResults.SelectMany(r => NarrationVerifier.NumbersIn(r.Json));
}

public sealed record ToolResult(string Tool, string Json);

/// <summary>
/// Tactical analyst agent on the reasoning model. Explains why an important moment matters, looking up stats through
/// code tools rather than computing anything itself.
/// </summary>
public sealed class TacticalAnalyst(ChatClient chatClient, ILogger<TacticalAnalyst> logger)
{
    public const string Instructions = """
        You are the tactical analyst in a live football broadcast studio. You receive one important moment as facts.
        In at most 40 words, explain WHY it matters for this match: the tactical story (chance quality, pressing,
        control of the ball, momentum, how the game state has changed). Use your tools to look up match stats,
        recent moments and player stats before writing. Every number you write must come from the facts or from a
        tool result, with the same unit. Never invent history, totals or events.
        Reply with JSON only: {"insight": "..."}
        """;

    public async Task<Insight?> AnalyseAsync(MomentFacts facts, StudioContext context, CancellationToken ct)
    {
        var results = new List<ToolResult>();
        string Record(string tool, object result)
        {
            var json = JsonSerializer.Serialize(result, MatchDirectorJson.Options);
            lock (results)
                results.Add(new ToolResult(tool, json));
            return json;
        }

        AITool[] tools =
        [
            AIFunctionFactory.Create(
                [Description("Team stats so far for both sides: goals, xG, shots, possession %, pass accuracy %, progressive passes, pressures, tackles, interceptions; plus momentum (-100..100, positive = home on top) and chaos (0-100).")]
                () => Record("get_match_stats", new
                {
                    home = new { team = context.Info.Home.Name, stats = context.Snapshot.Home },
                    away = new { team = context.Info.Away.Name, stats = context.Snapshot.Away },
                    context.Snapshot.Momentum,
                    context.Snapshot.Chaos,
                    minute = (int)context.Snapshot.MatchClock.TotalMinutes + 1
                }),
                "get_match_stats"),
            AIFunctionFactory.Create(
                [Description("The most recent detected moments (newest last) with minute, type, team, player and metrics.")]
                ([Description("How many recent moments to return, 1-10.")] int count) =>
                    Record("get_recent_moments", context.Recent.TakeLast(Math.Clamp(count, 1, 10))),
                "get_recent_moments"),
            AIFunctionFactory.Create(
                [Description("Stats so far for one player, by name as it appears in the facts.")]
                ([Description("Player name, e.g. \"Dario Okonkwe\".")] string name) =>
                {
                    var player = context.Info.Home.Players.Concat(context.Info.Away.Players)
                        .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    var stats = context.Snapshot.Players.FirstOrDefault(p => p.PlayerId == player?.PlayerId);
                    return Record("get_player_stats", player is null ? new { error = "unknown player" } : new { player.Name, player.Position, stats });
                },
                "get_player_stats")
        ];

        var agent = StudioFactory.Instrument(chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "TacticalAnalyst",
            ChatOptions = new ChatOptions
            {
                Instructions = Instructions,
                Tools = tools,
                Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low }
            }
        }, clientFactory: StudioFactory.TraceModelCalls));

        var prompt = JsonSerializer.Serialize(new
        {
            moment = facts.Moment.Type.ToString(),
            meaning = AgentNarrator.Meaning(facts),
            minute = facts.Minute,
            team = facts.Team,
            opponent = facts.Opponent,
            player = facts.Player,
            score = new { home = facts.HomeTeam, homeGoals = facts.HomeGoals, away = facts.AwayTeam, awayGoals = facts.AwayGoals },
            metrics = facts.Moment.Metrics,
            units = facts.Moment.Metrics.Keys.ToDictionary(k => k, AgentNarrator.Unit)
        }, MatchDirectorJson.Options);

        try
        {
            var response = await agent.RunAsync(prompt, cancellationToken: ct);
            if (AgentJson.Parse<Reply>(response.Text) is { Insight.Length: > 0 } reply)
                return new Insight(reply.Insight, results.ToList());
            logger.LogWarning("Analyst returned unusable output for {MomentId}", facts.Moment.MomentId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Analyst failed for {MomentId}", facts.Moment.MomentId);
        }
        return null;
    }

    private sealed record Reply(string Insight);
}
