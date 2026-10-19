using MatchDirector.Agents.Studio;
using MatchDirector.Contracts;
using MatchDirector.Simulator.Engine;

namespace MatchDirector.Eval;

/// <summary>The real studio pipeline on live models, one moment at a time (nothing is dropped, unlike the live queues).</summary>
public sealed record LiveResult(
    int Matches, int Moments, int AgentNarrations, int NumberCheckRejections, int Reviewed, int CriticApproved, int CriticFlagged,
    int Rewritten, int TemplateAfterReview, int InsightsRequested, int InsightsKept, int InsightsDropped,
    Latency Narrator, Latency Analyst, Latency Critic, IReadOnlyList<string> SampleIssues, IReadOnlyList<string> SampleInsights,
    IReadOnlyList<RecapOutcome> Recaps);

/// <summary>How one full-time report came out: who wrote it and what the checks said.</summary>
public sealed record RecapOutcome(string MatchId, string Headline, string Source, IReadOnlyList<MatchDirector.Contracts.AgentStep> Trace, string Casual);

public sealed record Latency(int Count, int P50, int P95)
{
    public static Latency Of(IEnumerable<int> values)
    {
        var sorted = values.Order().ToList();
        int At(double q) => sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(q * sorted.Count) - 1)];
        return new Latency(sorted.Count, At(0.5), At(0.95));
    }
}

public static class LiveEval
{
    public static async Task<LiveResult> RunAsync(StudioPipeline pipeline, EvalOptions options)
    {
        int moments = 0, agent = 0, rejected = 0, reviewed = 0, approved = 0, flagged = 0, rewritten = 0, template = 0;
        int insightsRequested = 0, insightsKept = 0, insightsDropped = 0;
        var narrator = new List<int>();
        var analyst = new List<int>();
        var critic = new List<int>();
        var issues = new List<string>();
        var insights = new List<string>();
        var recaps = new List<RecapOutcome>();

        for (var i = 0; i < options.LiveMatches; i++)
        {
            var match = new MatchEngine(options.FirstSeed + i, Clubs.KestrelBay, Clubs.RedmoorRovers).Play();
            var session = new MatchSession(match.Info);
            var done = 0;

            foreach (var e in match.Events)
            {
                foreach (var (moment, facts, context) in session.Ingest(e))
                {
                    if (done++ >= options.MaxMoments)
                        break;
                    moments++;
                    var aired = await pipeline.AirAsync(match.MatchId, moment, facts, CancellationToken.None);
                    narrator.Add(Step(aired.Overlay, "Narrator")!.DurationMs);
                    if (aired.Trace.Any(s => s.Agent == "Number check" && s.Outcome.StartsWith("rejected")))
                        rejected++;
                    if (aired.Narration.Source == "agent")
                        agent++;
                    if (!pipeline.ShouldReview(moment, aired))
                        continue;

                    reviewed++;
                    if (moment.Priority >= StudioPipeline.InsightPriority)
                        insightsRequested++;
                    var result = await pipeline.ReviewAsync(match.MatchId, moment, facts, context, aired, CancellationToken.None);
                    if (result.FirstReview.Pass) approved++; else flagged++;
                    if (result.Rewritten) rewritten++;
                    if (result.UsedTemplate) template++;
                    if (result.Insight is not null) insightsKept++;
                    if (result.InsightDropped) insightsDropped++;
                    if (Step(result.Overlay, "Tactical analyst") is { } a) analyst.Add(a.DurationMs);
                    critic.AddRange(result.Overlay.Trace!.Where(s => s.Agent.StartsWith("Verifier")).Select(s => s.DurationMs));
                    issues.AddRange(result.FirstReview.Issues.Select(x => $"{moment.Type}: {x}"));
                    if (result.Insight is { } insight)
                        insights.Add($"{moment.Type} ({facts.Minute}'): {insight.Text}");
                    Console.Write(result.FirstReview.Pass ? "✓" : "✎");
                }
            }
            // Every event has been ingested (the moment cap only limits narration), so this is the real full-time state.
            var recap = await pipeline.RecapAsync(session.RecapFacts(), CancellationToken.None);
            recaps.Add(new RecapOutcome(match.MatchId, recap.Headline, recap.Variants["source"], recap.Trace!, recap.Variants["casual"]));
            Console.WriteLine($" recap [{recap.Variants["source"]}]: {recap.Headline}");
        }

        return new LiveResult(options.LiveMatches, moments, agent, rejected, reviewed, approved, flagged, rewritten, template,
            insightsRequested, insightsKept, insightsDropped, Latency.Of(narrator), Latency.Of(analyst), Latency.Of(critic),
            issues.Take(12).ToList(), insights.Take(6).ToList(), recaps);
    }

    private static AgentStep? Step(Overlay overlay, string agent) => overlay.Trace?.FirstOrDefault(s => s.Agent == agent);
}
