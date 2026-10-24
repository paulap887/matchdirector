using MatchDirector.Agents.Studio;
using MatchDirector.Agents.Translation;
using MatchDirector.Simulator.Engine;

namespace MatchDirector.Eval;

/// <summary>How the translator and its number check did in each language.</summary>
public sealed record TranslationResult(int Moments, IReadOnlyList<LanguageOutcome> Languages, IReadOnlyList<string> Samples, IReadOnlyList<string> Rejections);

public sealed record LanguageOutcome(string Code, string Name, int Lines, int Kept, Latency Latency);

/// <summary>Translates checked English lines (headline moments and a full-time report) into every supported language.</summary>
public static class TranslationEval
{
    private static readonly string[] Lines = ["casual", "analyst", "homeFan", "awayFan", "insight"];

    public static async Task<TranslationResult> RunAsync(StudioPipeline pipeline, EvalOptions options)
    {
        var match = new MatchEngine(options.FirstSeed, Clubs.KestrelBay, Clubs.RedmoorRovers).Play();
        var session = new MatchSession(match.Info);
        var english = new List<IReadOnlyDictionary<string, string>>();
        foreach (var e in match.Events)
        {
            foreach (var (moment, facts, _) in session.Ingest(e))
            {
                if (english.Count >= options.Translate || moment.Priority < StudioPipeline.ReviewPriority)
                    continue;
                var aired = await pipeline.AirAsync(match.MatchId, moment, facts, CancellationToken.None);
                english.Add(Lines.Where(aired.Overlay.Variants.ContainsKey).ToDictionary(k => k, k => aired.Overlay.Variants[k]));
            }
        }
        var recap = await pipeline.RecapAsync(session.RecapFacts(), CancellationToken.None);
        english.Add(new Dictionary<string, string>
        {
            ["headline"] = recap.Headline, ["casual"] = recap.Variants["casual"], ["analyst"] = recap.Variants["analyst"],
            ["homeFan"] = recap.Variants["homeFan"], ["awayFan"] = recap.Variants["awayFan"]
        });

        var outcomes = new List<LanguageOutcome>();
        var samples = new List<string>();
        var rejections = new List<string>();
        foreach (var language in Languages.Supported)
        {
            int lines = 0, kept = 0;
            var latency = new List<int>();
            foreach (var item in english)
            {
                var translated = await pipeline.TranslateAsync(language, item, CancellationToken.None);
                lines += item.Count;
                kept += translated.Lines.Count;
                latency.Add(translated.Trace.FirstOrDefault()?.DurationMs ?? 0);
                if (translated.Trace.Skip(1).FirstOrDefault()?.Detail is { } detail && rejections.Count < 8)
                    rejections.Add($"{language.Name}: {detail}");
                if (item == english[0] && translated.Lines.GetValueOrDefault("casual") is { } line)
                    samples.Add($"{language.Name}: \"{item["casual"]}\" → \"{line}\"");
                Console.Write(translated.Lines.Count == item.Count ? "✓" : "·");
            }
            outcomes.Add(new LanguageOutcome(language.Code, language.Name, lines, kept, Latency.Of(latency)));
            Console.WriteLine($" {language.Code}: {kept}/{lines}");
        }
        return new TranslationResult(english.Count - 1, outcomes, samples, rejections);
    }
}
