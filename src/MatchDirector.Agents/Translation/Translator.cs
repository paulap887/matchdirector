using System.Text.Json;
using MatchDirector.Agents.Studio;
using MatchDirector.Contracts;
using Microsoft.Agents.AI;

namespace MatchDirector.Agents.Translation;

/// <summary>
/// Translator agent (Microsoft Agent Framework) on the fast model. It translates lines that have already passed every
/// check; <see cref="TranslationCheck"/> then makes sure each translation carries exactly the same numbers.
/// </summary>
public sealed class Translator(AIAgent agent, ILogger<Translator> logger)
{
    public const string Instructions = """
        You translate live football broadcast lines from English. You receive JSON: {"language": "...", "lines": {"<key>": "<English text>"}}.
        Translate every line into the target language as a native football commentator or supporter would say it: same
        meaning, same tone and a similar length. A fan line stays partisan, and "we"/"us" stays the fan's own club.
        Rules: keep every number exactly, written as digits (a score stays "2-0", never "two-nil" or "zero"); never add,
        drop or change a number. You may use the target
        language's decimal comma (0,35) and percent spacing (35 %). Keep player, club and venue names exactly as given.
        Keep "xG" as xG. Add nothing that is not in the English line.
        Reply with JSON only, the same keys with the translated text: {"<key>": "..."}
        """;

    public async Task<IReadOnlyDictionary<string, string>?> TranslateAsync(Language language, IReadOnlyDictionary<string, string> lines, CancellationToken ct)
    {
        try
        {
            var response = await agent.RunAsync(JsonSerializer.Serialize(new { language = language.Name, lines }, MatchDirectorJson.Options), cancellationToken: ct);
            if (AgentJson.Parse<Dictionary<string, string>>(response.Text) is { } translated
                && lines.Keys.All(k => !string.IsNullOrWhiteSpace(translated.GetValueOrDefault(k))))
                return lines.Keys.ToDictionary(k => k, k => translated[k].Trim());
            logger.LogWarning("Translator returned unusable {Language} output", language.Code);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Translator failed for {Language}", language.Code);
        }
        return null;
    }
}
