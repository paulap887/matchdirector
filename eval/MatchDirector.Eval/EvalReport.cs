using System.Text;
using System.Text.Json;
using MatchDirector.Contracts;

namespace MatchDirector.Eval;

public sealed class EvalReport(EvalOptions options)
{
    public DeterministicResult? Deterministic { get; set; }
    public LiveResult? Live { get; set; }
    public RedTeamResult? RedTeam { get; set; }

    public async Task WriteAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Out))!);
        await File.WriteAllTextAsync($"{options.Out}.md", Markdown());
        await File.WriteAllTextAsync($"{options.Out}.json", JsonSerializer.Serialize(new
        {
            generated = DateTimeOffset.UtcNow,
            options = options with { Endpoint = options.Endpoint is null ? null : "(set)" },
            deterministic = Deterministic,
            live = Live,
            redTeam = RedTeam
        }, new JsonSerializerOptions(MatchDirectorJson.Options) { WriteIndented = true }));
    }

    /// <summary>The guarantees that hold by construction; anything below 100% is a bug.</summary>
    public IReadOnlyList<string> GuaranteeFailures()
    {
        var failures = new List<string>();
        if (Deterministic is { } d)
        {
            if (d.MomentsWithValidEvidence != d.Moments)
                failures.Add($"{d.Moments - d.MomentsWithValidEvidence} moments cite missing or future events");
            if (d.TemplatesPassingNumberCheck != d.TemplateNarrations)
                failures.Add($"{d.TemplateNarrations - d.TemplatesPassingNumberCheck} template narrations contain uncomputed numbers");
            if (d.GoalMoments != d.Goals)
                failures.Add($"{d.Goals - d.GoalMoments} goals have no goal moment");
            if (d.RecapsPassingNumberCheck != d.Recaps)
                failures.Add($"{d.Recaps - d.RecapsPassingNumberCheck} full-time template recaps contain uncomputed numbers");
        }
        if (RedTeam?.Attacks.FirstOrDefault(a => a.ExpectedCatcher == "code") is { } code && code.CaughtByCode != code.Samples)
            failures.Add($"{code.Samples - code.CaughtByCode} invented statistics passed the number check");
        return failures;
    }

    private static string Pct(int part, int whole) => whole == 0 ? "n/a" : $"{100.0 * part / whole:0.#}%";

    private string Markdown()
    {
        var md = new StringBuilder();
        md.AppendLine("# MatchDirector evaluation");
        md.AppendLine();
        md.AppendLine($"Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC by `eval/MatchDirector.Eval`. All matches are synthetic and seeded, so the deterministic results reproduce exactly; model results vary slightly between runs.");
        md.AppendLine();

        md.AppendLine("## Headlines");
        md.AppendLine();
        if (Deterministic is { } d)
        {
            md.AppendLine($"- **{Pct(d.MomentsWithValidEvidence, d.Moments)} of {d.Moments:N0} moments cite valid, earlier events** as evidence ({d.Matches} matches, {d.Events:N0} events).");
            md.AppendLine($"- **{Pct(d.TemplatesPassingNumberCheck, d.TemplateNarrations)} of fallback narrations pass the number check**, so the safe path never states an uncomputed number.");
        }
        if (RedTeam is { } r && r.Attacks.FirstOrDefault(a => a.ExpectedCatcher == "code") is { } invented)
            md.AppendLine($"- **{Pct(invented.CaughtByCode, invented.Samples)} of invented statistics are blocked by code** before anything reaches the screen.");
        if (RedTeam is { CriticEvaluated: true } rc)
        {
            var semantic = rc.Attacks.Where(a => a.ExpectedCatcher == "critic").ToList();
            md.AppendLine($"- **The verifier critic catches {Pct(semantic.Sum(a => a.Caught), semantic.Sum(a => a.Samples))} of meaning errors** (wrong team, swapped fan lines, wrong score story, invented history) with **{Pct(rc.CleanFlaggedByCritic, rc.CleanSamples)} false alarms** on correct lines.");
        }
        if (Live is { } l)
        {
            md.AppendLine($"- **Live studio:** the critic reviewed {l.Reviewed} moments, approved {l.CriticApproved} and flagged {l.CriticFlagged}. Flagged lines were rewritten and rechecked ({l.Rewritten} rewritten, {l.TemplateAfterReview} replaced by the template); flagged insights were dropped. Narrator latency p50 {l.Narrator.P50} ms, p95 {l.Narrator.P95} ms.");
        }
        md.AppendLine();

        if (Deterministic is { } det)
        {
            md.AppendLine("## Deterministic checks (no model)");
            md.AppendLine();
            md.AppendLine("| Check | Result |");
            md.AppendLine("|---|---|");
            md.AppendLine($"| Matches simulated | {det.Matches} ({det.Events:N0} events) |");
            md.AppendLine($"| Moments with valid evidence | {det.MomentsWithValidEvidence:N0} / {det.Moments:N0} ({Pct(det.MomentsWithValidEvidence, det.Moments)}) |");
            md.AppendLine($"| Goals with a goal moment | {det.GoalMoments} / {det.Goals} ({Pct(det.GoalMoments, det.Goals)}) |");
            md.AppendLine($"| Template narrations passing the number check | {det.TemplatesPassingNumberCheck:N0} / {det.TemplateNarrations:N0} ({Pct(det.TemplatesPassingNumberCheck, det.TemplateNarrations)}) |");
            md.AppendLine($"| Full-time template recaps passing the number check | {det.RecapsPassingNumberCheck} / {det.Recaps} ({Pct(det.RecapsPassingNumberCheck, det.Recaps)}) |");
            md.AppendLine($"| Stats engine throughput | {det.EventsPerSecond:N0} events/second |");
            md.AppendLine($"| Realism per match | {det.MeanGoals} goals, {det.MeanShots} shots, {det.MeanPassAccuracy}% pass accuracy |");
            md.AppendLine();
            md.AppendLine("Moments per match: " + string.Join(", ", det.MomentsPerMatch.Select(m => $"{m.Key} {m.Value}")) + ".");
            md.AppendLine();
        }

        if (RedTeam is { } red)
        {
            md.AppendLine("## Red team: deliberately wrong narration");
            md.AppendLine();
            md.AppendLine($"Each attack edits correct (template) lines for {red.Moments} non-routine moments. Code checks numbers first; the critic sees only what passes.");
            md.AppendLine();
            md.AppendLine("| Attack | What it does | Expected catcher | Caught by code | Caught by critic | Total caught |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var a in red.Attacks)
                md.AppendLine($"| {a.Name} | {a.Description} | {a.ExpectedCatcher} | {a.CaughtByCode}/{a.Samples} | {(red.CriticEvaluated ? $"{a.CaughtByCritic}/{a.Samples}" : "not run")} | {Pct(a.Caught, a.Samples)} |");
            md.AppendLine();
            md.AppendLine(red.CriticEvaluated
                ? $"False alarms: the critic flagged {red.CleanFlaggedByCritic} of {red.CleanSamples} correct narrations ({Pct(red.CleanFlaggedByCritic, red.CleanSamples)})."
                : "The critic was not run (no Foundry endpoint), so only the code check is measured.");
            md.AppendLine();
            if (red.FalseAlarms.Count > 0)
            {
                md.AppendLine("What the critic objected to in correct lines:");
                md.AppendLine();
                foreach (var alarm in red.FalseAlarms.Take(10))
                    md.AppendLine($"- {alarm}");
                md.AppendLine();
            }
        }

        if (Live is { } live)
        {
            md.AppendLine($"## Live studio ({options.FastModel} narrator, {options.ReasoningModel} analyst and critic)");
            md.AppendLine();
            md.AppendLine("| Measure | Result |");
            md.AppendLine("|---|---|");
            md.AppendLine($"| Moments narrated | {live.Moments} over {live.Matches} matches ({live.AgentNarrations} by the agent) |");
            md.AppendLine($"| Narrations rejected by the number check | {live.NumberCheckRejections} ({Pct(live.NumberCheckRejections, live.Moments)}) |");
            md.AppendLine($"| Reviewed by the critic | {live.Reviewed}: {live.CriticApproved} approved, {live.CriticFlagged} flagged |");
            md.AppendLine($"| Flagged moments rewritten / template after second review | {live.Rewritten} / {live.TemplateAfterReview} |");
            md.AppendLine($"| Reviewed moments with a fan-line issue | {live.FanLineIssues} of {live.Reviewed} ({Pct(live.FanLineIssues, live.Reviewed)}) |");
            md.AppendLine($"| Tactical insights | {live.InsightsKept} kept of {live.InsightsRequested} requested ({live.InsightsDropped} dropped by checks) |");
            md.AppendLine($"| Latency p50 / p95 | narrator {live.Narrator.P50} / {live.Narrator.P95} ms · analyst {live.Analyst.P50} / {live.Analyst.P95} ms · critic {live.Critic.P50} / {live.Critic.P95} ms |");
            md.AppendLine();
            if (live.Recaps.Count > 0)
            {
                md.AppendLine("Full-time reports:");
                md.AppendLine();
                foreach (var report in live.Recaps)
                {
                    var checks = string.Join(" → ", report.Trace.Select(t => $"{t.Agent}: {t.Outcome}"));
                    md.AppendLine($"- **{report.Headline}** ({report.Source}). {report.Casual} _Checks: {checks}._");
                }
                md.AppendLine();
            }
            if (live.SampleIssues.Count > 0)
            {
                md.AppendLine("Examples of what the critic flagged:");
                md.AppendLine();
                foreach (var issue in live.SampleIssues)
                    md.AppendLine($"- {issue}");
                md.AppendLine();
            }
            if (live.SampleInsights.Count > 0)
            {
                md.AppendLine("Examples of tactical insights that passed both checks:");
                md.AppendLine();
                foreach (var insight in live.SampleInsights)
                    md.AppendLine($"- {insight}");
                md.AppendLine();
            }
        }

        md.AppendLine("## Reproduce");
        md.AppendLine();
        md.AppendLine("```bash");
        md.AppendLine("# deterministic checks and the code half of the red team (no Azure needed)");
        md.AppendLine("dotnet run --project eval/MatchDirector.Eval");
        md.AppendLine("# full run against your Foundry project (Azure CLI login)");
        md.AppendLine("dotnet run --project eval/MatchDirector.Eval -- --endpoint https://<your-foundry>.cognitiveservices.azure.com/");
        md.AppendLine("```");
        return md.ToString();
    }
}
