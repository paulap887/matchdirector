using System.Globalization;
using System.Text.RegularExpressions;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents.Narration;

/// <summary>The same moment told two ways: for a casual fan and for an analyst.</summary>
public sealed record Narrated(string Casual, string Analyst, string Source);

public interface INarrator
{
    Task<Narrated> NarrateAsync(MomentFacts facts, CancellationToken cancellationToken);
}

/// <summary>
/// Code-based verifier: rejects narration that contains any number not present in the computed facts,
/// so a model can never introduce a statistic of its own.
/// </summary>
public static partial class NarrationVerifier
{
    public static IReadOnlyList<string> UnsupportedNumbers(Narrated narration, MomentFacts facts)
    {
        var allowed = facts.AllowedNumbers();
        return Numbers().Matches($"{narration.Casual} {narration.Analyst}")
            .Select(m => m.Value)
            .Where(n => !allowed.Contains(double.Parse(n, CultureInfo.InvariantCulture)))
            .Distinct()
            .ToList();
    }

    [GeneratedRegex(@"\d+(?:\.\d+)?")]
    private static partial Regex Numbers();
}

/// <summary>Deterministic narration used when no model is configured, and as the fallback when a model's text fails verification.</summary>
public sealed class TemplateNarrator : INarrator
{
    public Task<Narrated> NarrateAsync(MomentFacts f, CancellationToken cancellationToken) =>
        Task.FromResult(Narrate(f));

    public static Narrated Narrate(MomentFacts f)
    {
        var m = f.Moment.Metrics;
        string Metric(string key, string format = "0.##") => m.TryGetValue(key, out var v) ? v.ToString(format, CultureInfo.InvariantCulture) : "?";
        var who = f.Player ?? f.Team;
        var score = $"{f.HomeTeam} {f.HomeGoals}-{f.AwayGoals} {f.AwayTeam}";

        var (casual, analyst) = f.Moment.Type switch
        {
            MomentType.Goal => ($"GOAL! {who} scores for {f.Team}. {score}.",
                $"{who} finishes a {Metric("passesInMove")}-pass move from {Metric("distance", "0")} m (xG {Metric("xg")})."),
            MomentType.BigChanceMissed => ($"So close! {who} misses a big chance for {f.Team}.",
                $"Chance worth {Metric("xg")} xG from {Metric("distance", "0")} m goes begging."),
            MomentType.KeySave => ($"What a save to deny {f.Team}!",
                $"Keeper stops a {Metric("xg")} xG effort from {Metric("distance", "0")} m."),
            MomentType.PassingMove => ($"{f.Team} are keeping the ball beautifully.",
                $"{Metric("passes")} straight passes involving {Metric("players")} players in {Metric("seconds")} seconds."),
            MomentType.DifficultPass => ($"Brilliant pass from {who}!",
                $"{Metric("distance", "0")} m pass rated {Metric("difficulty")} difficulty, gaining {Metric("progression", "0")} m."),
            MomentType.HighRegain => ($"{f.Team} win it back high up the pitch!",
                $"Regain {Metric("distanceToGoal", "0")} m from goal: pressing is paying off."),
            MomentType.MomentumShift => ($"{f.Team} are taking control of this game.",
                $"Momentum swings to {f.Team} ({Metric("momentum", "0")})."),
            MomentType.TempoShift => (m.GetValueOrDefault("chaos") >= 50 ? "This has turned end-to-end!" : "The game has settled down.",
                $"Control-vs-chaos index now {Metric("chaos", "0")}."),
            MomentType.HalfTime => ($"Half-time: {score}.",
                $"xG {Metric("homeXg")} v {Metric("awayXg")}, possession {Metric("homePossession", "0")}% v {Metric("awayPossession", "0")}%."),
            MomentType.FullTime => ($"Full-time: {score}.",
                $"xG {Metric("homeXg")} v {Metric("awayXg")}, shots {Metric("homeShots")} v {Metric("awayShots")}."),
            _ => ($"{f.Team}: {f.Moment.Type}.", $"{f.Moment.Type} at minute {f.Minute}.")
        };
        return new Narrated(casual, analyst, "template");
    }
}
