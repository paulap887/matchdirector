using System.Globalization;
using System.Text.RegularExpressions;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents.Narration;

/// <summary>The same moment told for four audiences: a casual fan, an analyst, and a supporter of each club.</summary>
public sealed record Narrated(string Casual, string Analyst, string HomeFan, string AwayFan, string Source)
{
    public IEnumerable<string> Lines => [Casual, Analyst, HomeFan, AwayFan];
}

public interface INarrator
{
    Task<Narrated> NarrateAsync(MomentFacts facts, CancellationToken cancellationToken);

    /// <summary>Rewrites a narration to fix the issues a reviewer found.</summary>
    Task<Narrated> ReviseAsync(MomentFacts facts, Narrated previous, IReadOnlyList<string> issues, CancellationToken cancellationToken);
}

/// <summary>
/// Code-based verifier: rejects narration that contains any number not present in the computed facts,
/// so a model can never introduce a statistic of its own.
/// </summary>
public static partial class NarrationVerifier
{
    public static IReadOnlyList<string> UnsupportedNumbers(Narrated narration, MomentFacts facts) =>
        UnsupportedNumbers(narration.Lines, facts.AllowedNumbers());

    public static IReadOnlyList<string> UnsupportedNumbers(IEnumerable<string> lines, IReadOnlySet<double> allowed) =>
        Numbers().Matches(string.Join(" ", lines))
            .Select(m => m.Value)
            .Where(n => !allowed.Contains(double.Parse(n, CultureInfo.InvariantCulture)))
            .Distinct()
            .ToList();

    /// <summary>Numbers that appear in a piece of text, such as a tool result.</summary>
    public static IEnumerable<double> NumbersIn(string text) =>
        Numbers().Matches(text).Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture));

    [GeneratedRegex(@"\d+(?:\.\d+)?")]
    private static partial Regex Numbers();
}

/// <summary>Deterministic narration used when no model is configured, and as the fallback when a model's text fails verification.</summary>
public sealed class TemplateNarrator : INarrator
{
    public Task<Narrated> NarrateAsync(MomentFacts f, CancellationToken cancellationToken) =>
        Task.FromResult(Narrate(f));

    public Task<Narrated> ReviseAsync(MomentFacts f, Narrated previous, IReadOnlyList<string> issues, CancellationToken cancellationToken) =>
        Task.FromResult(Narrate(f));

    public static Narrated Narrate(MomentFacts f)
    {
        var m = f.Moment.Metrics;
        string Metric(string key, string format = "0.##") => m.TryGetValue(key, out var v) ? v.ToString(format, CultureInfo.InvariantCulture) : "?";
        var who = f.Player ?? f.Team;
        var score = $"{f.HomeTeam} {f.HomeGoals}-{f.AwayGoals} {f.AwayTeam}";

        var tally = m.GetValueOrDefault("playerGoals") switch { 2 => " That's a brace!", >= 3 => " Hat-trick!", _ => "" };
        var (casual, analyst) = f.Moment.Type switch
        {
            MomentType.Goal => ($"GOAL! {who} scores for {f.Team}. {score}.{tally}",
                $"{who} finishes a {Metric("passesInMove")}-pass move from {Metric("distance", "0")} m (xG {Metric("xg")})."),
            MomentType.BigChanceMissed => ($"So close! {who} misses a big chance for {f.Team}.",
                $"Chance worth {Metric("xg")} xG from {Metric("distance", "0")} m goes begging."),
            MomentType.KeySave => ($"What a save from {who} to keep {f.Opponent} out!",
                $"Keeper stops a {Metric("xg")} xG effort from {Metric("distance", "0")} m."),
            MomentType.PassingMove => ($"{f.Team} are keeping the ball beautifully.",
                $"{Metric("passes")} straight passes involving {Metric("players")} players in {Metric("seconds")} seconds."),
            MomentType.DifficultPass => ($"Brilliant pass from {who}!",
                $"{Metric("distance", "0")} m pass rated {Metric("difficulty")} difficulty, gaining {Metric("progression", "0")} m."),
            MomentType.HighRegain => ($"{f.Team} win it back high up the pitch!",
                $"Regain {Metric("distanceToGoal", "0")} m from goal: pressing is paying off."),
            MomentType.RocketShot => ($"What a strike from {who}!",
                $"Shot clocked at {Metric("shotSpeed", "0.#")} m/s from {Metric("distance", "0")} m, reaching the {Metric("speedThreshold", "0")} m/s alert threshold."),
            MomentType.DrivingRun => ($"{who} drives forward at pace!",
                $"Carry of {Metric("distance", "0")} m at {Metric("speed", "0.#")} m/s, reaching the {Metric("speedThreshold", "0.#")} m/s alert threshold."),
            MomentType.DistanceMilestone => ($"{who} keeps carrying {f.Team} forward.",
                $"{who} passes {Metric("metresCarried", "0")} m carried with the ball this match."),
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
        return new Narrated(casual, analyst, Fan(f, home: true), Fan(f, home: false), "template");
    }

    /// <summary>A partisan line from the point of view of one club's supporters.</summary>
    private static string Fan(MomentFacts f, bool home)
    {
        var ours = f.Team == (home ? f.HomeTeam : f.AwayTeam);
        var who = f.Player ?? f.Team;
        var (us, them) = home ? (f.HomeGoals, f.AwayGoals) : (f.AwayGoals, f.HomeGoals);
        var result = us > them ? "We're winning" : us < them ? "We're behind" : "All square";

        return f.Moment.Type switch
        {
            MomentType.Goal => ours ? $"YES! {who} scores! {us}-{them}!" : $"Gutted. {who} scores for {f.Team}. {us}-{them}.",
            MomentType.RocketShot => ours ? $"What a hit from {who}! Keep shooting!" : $"Phew, {who} nearly broke the net.",
            MomentType.DrivingRun => ours ? $"Go on {who}, run at them!" : $"{who} is running at us. Somebody close that down!",
            MomentType.DistanceMilestone => ours ? $"{who} never stops driving us on!" : $"{who} keeps running at us all game.",
            MomentType.BigChanceMissed => ours ? $"Agony! {who} should have scored there." : $"Phew! {f.Team} waste a big chance.",
            MomentType.KeySave => ours ? $"Get in! {who} keeps them out!" : $"Denied by {who}. So close!",
            MomentType.PassingMove => ours ? "Lovely stuff, we're keeping the ball!" : $"{f.Team} have the ball and we can't get it back.",
            MomentType.DifficultPass => ours ? $"What a ball from {who}!" : $"Dangerous pass from {who}, watch out.",
            MomentType.HighRegain => ours ? "Great pressing, we win it back high up!" : $"Sloppy, {f.Team} nick it off us.",
            MomentType.MomentumShift => ours ? "We're on top now, come on!" : $"{f.Team} are taking over. Hold on!",
            MomentType.TempoShift => f.Moment.Metrics.GetValueOrDefault("chaos") >= 50 ? "End to end now, nerves everywhere!" : "Calmer now. Keep the ball!",
            MomentType.HalfTime => $"Half-time. {result}, {us}-{them}.",
            MomentType.FullTime => us > them ? $"Full-time: we win {us}-{them}!" : us < them ? $"Full-time: beaten {us}-{them}." : $"Full-time: a {us}-{them} draw.",
            _ => $"{f.Moment.Type} for {f.Team}."
        };
    }
}
