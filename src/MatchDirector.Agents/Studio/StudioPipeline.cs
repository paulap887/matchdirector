using System.Diagnostics;
using MatchDirector.Agents.Narration;
using MatchDirector.Agents.Translation;
using MatchDirector.Contracts;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents.Studio;

/// <summary>An overlay as first put on air by the fast lane.</summary>
public sealed record Aired(Overlay Overlay, Narrated Narration, IReadOnlyList<AgentStep> Trace);

/// <summary>An overlay after the review lane, with what the reviewers did to it.</summary>
public sealed record Reviewed(Overlay Overlay, Review FirstReview, bool Rewritten, bool UsedTemplate, Insight? Insight, bool InsightDropped);

/// <summary>
/// What the studio does with one moment. The fast lane narrates and checks numbers; the review lane adds the tactical
/// analyst's insight, fact-checks with the verifier critic and rewrites what it flags. The director runs these on its
/// live queues; the evaluation harness runs them one moment at a time.
/// </summary>
public sealed class StudioPipeline(INarrator narrator, StudioTeam studio, ILogger<StudioPipeline> logger)
{
    public static readonly ActivitySource Activity = new("MatchDirector.Agents");

    /// <summary>Moments at or above this priority are fact-checked by the critic (routine ones rely on the code check).</summary>
    public const int ReviewPriority = 45;

    /// <summary>Moments at or above this priority also get a tactical insight.</summary>
    public const int InsightPriority = 55;

    public StudioTeam Studio => studio;

    public bool ShouldReview(Moment moment, Aired aired) =>
        studio.HasReviewers && aired.Narration.Source == "agent" && moment.Priority >= ReviewPriority;

    /// <summary>Fast lane: narrate, check every number, build the overlay.</summary>
    public async Task<Aired> AirAsync(string matchId, Moment moment, MomentFacts facts, CancellationToken ct)
    {
        using var activity = Activity.StartActivity("narrate-moment");
        activity?.SetTag("moment.id", moment.MomentId);
        activity?.SetTag("moment.type", moment.Type.ToString());

        var trace = new List<AgentStep>();
        var started = Stopwatch.GetTimestamp();
        var narration = await narrator.NarrateAsync(facts, ct);
        trace.Add(new("Narrator", studio.FastModel, narration.Source == "agent" ? "wrote 4 audience lines" : "template (model unavailable)", Ms(started)));
        narration = CheckNumbers(narration, facts, trace);
        activity?.SetTag("narration.source", narration.Source);

        return new Aired(ToOverlay(matchId, moment, narration, null, trace), narration, trace);
    }

    /// <summary>Review lane: tactical insight, fact-check, rewrite and recheck, then the republished overlay.</summary>
    public async Task<Reviewed> ReviewAsync(string matchId, Moment moment, MomentFacts facts, StudioContext context, Aired aired, CancellationToken ct)
    {
        if (studio.Critic is not { } critic)
            throw new InvalidOperationException("The review lane needs a verifier critic.");

        using var activity = Activity.StartActivity("review-moment");
        activity?.SetTag("moment.id", moment.MomentId);
        var trace = aired.Trace.ToList();
        var narration = aired.Narration;
        Insight? insight = null;
        var insightDropped = false;

        if (studio.Analyst is { } analyst && moment.Priority >= InsightPriority)
        {
            var started = Stopwatch.GetTimestamp();
            if (await analyst.AnalyseAsync(facts, context, ct) is { } result)
            {
                var invented = NarrationVerifier.UnsupportedNumbers([result.Text], facts.AllowedNumbers(result.ToolNumbers));
                insight = invented.Count == 0 ? result : null;
                insightDropped = invented.Count > 0;
                trace.Add(new("Tactical analyst", studio.ReasoningModel,
                    invented.Count == 0 ? $"insight using {string.Join(", ", result.ToolsUsed.DefaultIfEmpty("the facts only"))}" : "insight dropped by number check",
                    Ms(started), invented.Count == 0 ? null : $"unsupported numbers: {string.Join(", ", invented)}"));
            }
            else
            {
                trace.Add(new("Tactical analyst", studio.ReasoningModel, "no insight", Ms(started)));
            }
        }

        // Fact-check, then let the narrator fix what was flagged. A rewrite is checked again; if it still fails
        // after the second round, the deterministic template goes on air instead.
        Review? first = null;
        var rewritten = false;
        var usedTemplate = false;
        for (var round = 1; round <= 2; round++)
        {
            var reviewed = Stopwatch.GetTimestamp();
            var review = await critic.ReviewAsync(facts, narration, insight, ct);
            first ??= review;
            trace.Add(new(round == 1 ? "Verifier critic" : "Verifier critic (recheck)", studio.ReasoningModel,
                review.Pass ? "approved" : $"flagged {review.Issues.Count} issue(s)", Ms(reviewed),
                review.Pass ? null : string.Join(" · ", review.Issues)));
            if (review.Pass)
                break;

            if (review.Issues.Any(IsAboutInsight) && insight is not null)
            {
                insight = null;
                insightDropped = true;
            }
            var lineIssues = review.Issues.Where(i => !IsAboutInsight(i)).ToList();
            if (lineIssues.Count == 0)
                break;

            if (round == 2)
            {
                narration = TemplateNarrator.Narrate(facts);
                usedTemplate = true;
                trace.Add(new("Director", "code", "used template after second failed review", 0));
                break;
            }

            var revised = Stopwatch.GetTimestamp();
            narration = await narrator.ReviseAsync(facts, narration, lineIssues, ct);
            rewritten = true;
            trace.Add(new("Narrator", studio.FastModel, narration.Source == "agent" ? "rewrote lines after review" : "template after failed rewrite", Ms(revised)));
            narration = CheckNumbers(narration, facts, trace);
            if (narration.Source != "agent")
            {
                usedTemplate = true;
                break; // the template is deterministic; nothing for the critic to recheck
            }
        }

        activity?.SetTag("review.pass", first!.Pass); // the loop always runs at least once
        return new Reviewed(ToOverlay(matchId, moment, narration, insight?.Text, trace), first!, rewritten, usedTemplate, insight, insightDropped);
    }

    /// <summary>
    /// Full-time report: the recap writer drafts it, code checks every number, the critic checks the meaning.
    /// Anything that fails a check is replaced by the deterministic template report.
    /// </summary>
    public async Task<MatchRecap> RecapAsync(Recap.RecapFacts facts, CancellationToken ct)
    {
        using var activity = Activity.StartActivity("write-recap");
        activity?.SetTag("match.id", facts.Info.MatchId);
        var trace = new List<AgentStep>();
        var lines = Recap.TemplateRecap.Write(facts);

        if (studio.RecapWriter is { } writer)
        {
            var started = Stopwatch.GetTimestamp();
            if (await writer.WriteAsync(facts, ct) is { } drafted)
            {
                trace.Add(new("Recap writer", studio.ReasoningModel, "wrote headline and 4 reports", Ms(started)));
                var invented = NarrationVerifier.UnsupportedNumbers(drafted.All, facts.AllowedNumbers());
                if (invented.Count > 0)
                {
                    trace.Add(new("Number check", "code", "rejected; used template", 0, $"unsupported numbers: {string.Join(", ", invented)}"));
                }
                else
                {
                    trace.Add(new("Number check", "code", "every number traced to computed facts", 0));
                    var reviewed = Stopwatch.GetTimestamp();
                    var review = studio.Critic is { } critic ? await critic.ReviewRecapAsync(facts, drafted, ct) : Review.Skipped;
                    trace.Add(new("Verifier critic", studio.ReasoningModel, review.Pass ? "approved" : $"flagged {review.Issues.Count} issue(s); used template",
                        Ms(reviewed), review.Pass ? null : string.Join(" · ", review.Issues)));
                    if (review.Pass)
                        lines = drafted;
                }
            }
            else
            {
                trace.Add(new("Recap writer", studio.ReasoningModel, "no usable report; used template", Ms(started)));
            }
        }
        else
        {
            trace.Add(new("Recap writer", "code", "template report (no model configured)", 0));
        }

        activity?.SetTag("recap.source", lines.Source);
        return new MatchRecap(
            facts.Info.MatchId,
            lines.Headline,
            new Dictionary<string, string>
            {
                ["casual"] = lines.Casual,
                ["analyst"] = lines.Analyst,
                ["homeFan"] = lines.HomeFan,
                ["awayFan"] = lines.AwayFan,
                ["source"] = lines.Source
            },
            facts.Best?.PlayerId,
            facts.Metrics,
            facts.Evidence,
            trace);
    }

    /// <summary>
    /// Translation: the translator rewords checked English lines in one language, then code makes sure each line carries
    /// exactly the same numbers. A line that fails is left out, so viewers see the English line instead.
    /// </summary>
    public async Task<Translated> TranslateAsync(Language language, IReadOnlyDictionary<string, string> english, CancellationToken ct)
    {
        using var activity = Activity.StartActivity("translate");
        activity?.SetTag("translation.language", language.Code);
        var trace = new List<AgentStep>();
        var kept = new Dictionary<string, string>();
        if (studio.Translator is not { } translator)
            return new Translated(language.Code, english, kept, trace);

        var started = Stopwatch.GetTimestamp();
        var lines = await translator.TranslateAsync(language, english, ct);
        if (lines is null)
        {
            trace.Add(new($"Translator ({language.Code})", studio.FastModel, $"no usable {language.Name}; English shown", Ms(started)));
            return new Translated(language.Code, english, kept, trace);
        }
        trace.Add(new($"Translator ({language.Code})", studio.FastModel, $"translated {lines.Count} lines into {language.Name}", Ms(started)));

        var rejected = new List<string>();
        foreach (var (key, text) in lines)
        {
            var differences = TranslationCheck.Differences(english[key], text);
            if (differences.Count == 0)
                kept[key] = text;
            else
                rejected.Add($"{key}: {string.Join(", ", differences)}");
        }
        trace.Add(new($"Number check ({language.Code})", "code",
            rejected.Count == 0 ? "same numbers as the checked English" : $"kept {kept.Count} of {lines.Count} lines; English shown for the rest",
            0, rejected.Count == 0 ? null : string.Join(" · ", rejected)));
        activity?.SetTag("translation.kept", kept.Count);
        return new Translated(language.Code, english, kept, trace);
    }

    private static bool IsAboutInsight(string issue) => issue.StartsWith("insight", StringComparison.OrdinalIgnoreCase);

    private Narrated CheckNumbers(Narrated narration, MomentFacts facts, List<AgentStep> trace)
    {
        var unsupported = NarrationVerifier.UnsupportedNumbers(narration, facts);
        if (unsupported.Count == 0)
        {
            trace.Add(new("Number check", "code", "every number traced to computed facts", 0));
            return narration;
        }

        logger.LogWarning("Number check rejected {Source} narration for {MomentId}: {Numbers}", narration.Source, facts.Moment.MomentId, unsupported);
        trace.Add(new("Number check", "code", "rejected; used template", 0, $"unsupported numbers: {string.Join(", ", unsupported)}"));
        return TemplateNarrator.Narrate(facts);
    }

    private static int Ms(long started) => (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    private static Overlay ToOverlay(string matchId, Moment moment, Narrated narration, string? insight, IReadOnlyList<AgentStep> trace)
    {
        var variants = new Dictionary<string, string>
        {
            ["casual"] = narration.Casual,
            ["analyst"] = narration.Analyst,
            ["homeFan"] = narration.HomeFan,
            ["awayFan"] = narration.AwayFan,
            ["source"] = narration.Source
        };
        if (insight is not null)
            variants["insight"] = insight;

        return new Overlay(
            moment.MomentId,
            matchId,
            moment.MatchClock,
            moment.MatchClock,
            moment.Type switch
            {
                MomentType.Goal => 9000,
                MomentType.HalfTime or MomentType.FullTime => 10000,
                _ => 6000
            },
            moment.Type.ToString(),
            moment.Priority,
            moment.PlayerId,
            moment.Metrics,
            moment.Evidence,
            variants,
            moment.Participants,
            trace.ToList());
    }
}
