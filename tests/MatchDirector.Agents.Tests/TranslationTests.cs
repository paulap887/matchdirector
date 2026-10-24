using MatchDirector.Agents.Narration;
using MatchDirector.Agents.Studio;
using MatchDirector.Agents.Translation;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace MatchDirector.Agents.Tests;

public class TranslationTests
{
    private static readonly Language French = Languages.Find("fr")!;

    private static readonly Dictionary<string, string> English = new()
    {
        ["casual"] = "GOAL! Dario Okonkwe scores for Kestrel Bay FC. Kestrel Bay FC 2-1 Redmoor Rovers.",
        ["analyst"] = "Okonkwe finishes a 4-pass move from 14 m (xG 0.35), struck at 23.8 m/s."
    };

    [Theory]
    [InlineData("Okonkwe conclut une action de 4 passes à 14 m (xG 0,35), frappe à 23,8 m/s.")]
    [InlineData("Okonkwe remata a 23,8 m/s desde 14 m tras 4 pases (xG 0,35).")]
    [InlineData("Okonkwe schließt einen Angriff über 4 Pässe aus 14 m ab (xG 0.35), mit 23,8 m/s.")]
    public void A_faithful_translation_keeps_every_number_with_a_decimal_comma_or_point(string translated)
    {
        Assert.Empty(TranslationCheck.Differences(English["analyst"], translated));
    }

    [Fact]
    public void A_score_can_be_reworded_as_long_as_its_numbers_stay()
    {
        Assert.Empty(TranslationCheck.Differences(English["casual"], "BUT ! Dario Okonkwe marque pour Kestrel Bay FC. Kestrel Bay FC 2 à 1 Redmoor Rovers."));
    }

    [Theory]
    [InlineData("Okonkwe conclut 4 passes à 14 m (xG 0,35), à 23,8 m/s, son 12e but.", "added 12")]
    [InlineData("Okonkwe conclut une action de quatre passes à 14 m (xG 0,35), à 23,8 m/s.", "dropped 4")]
    [InlineData("Okonkwe conclut 4 passes à 14 m (xG 0,53), à 23,8 m/s.", "added 0.53")]
    public void A_translation_that_adds_drops_or_changes_a_number_is_caught(string translated, string difference)
    {
        Assert.Contains(difference, TranslationCheck.Differences(English["analyst"], translated));
    }

    [Fact]
    public async Task Lines_that_fail_the_check_are_left_out_so_viewers_read_the_English()
    {
        var pipeline = Pipeline("""
            {"casual": "BUT ! Dario Okonkwe marque pour Kestrel Bay FC. Kestrel Bay FC 2-1 Redmoor Rovers.",
             "analyst": "Okonkwe conclut 4 passes à 14 m (xG 0,35), à 23,8 m/s, son 12e but de la saison."}
            """);

        var translated = await pipeline.TranslateAsync(French, English, CancellationToken.None);

        Assert.Equal(["casual"], translated.Lines.Keys);
        Assert.Equal(["Translator (fr)", "Number check (fr)"], translated.Trace.Select(s => s.Agent));
        Assert.Equal("code", translated.Trace[1].Model);
        Assert.Contains("analyst: added 12", translated.Trace[1].Detail);
    }

    [Fact]
    public async Task An_unusable_reply_translates_nothing()
    {
        var translated = await Pipeline("Désolé, je ne peux pas.").TranslateAsync(French, English, CancellationToken.None);

        Assert.Empty(translated.Lines);
        Assert.Contains("English shown", Assert.Single(translated.Trace).Outcome);
    }

    [Fact]
    public void A_translation_of_lines_the_reviewers_replaced_is_discarded()
    {
        var book = new TranslationBook<Dictionary<string, string>>(_ => "m", lines => lines, kept: 10);
        book.Put("o1", English);
        var stale = new Translated("fr", English, new Dictionary<string, string> { ["casual"] = "BUT !" }, []);

        var corrected = new Dictionary<string, string>(English) { ["casual"] = "GOAL! Okonkwe makes it 2-1." };
        book.Put("o1", corrected);

        Assert.False(book.Add("o1", stale));
        Assert.Empty(book.Current("o1")!.Value.Translations);
        Assert.Equal([French], book.Pending("o1", [French])!.Value.Missing);
    }

    [Fact]
    public void A_translation_survives_a_republish_with_the_same_English()
    {
        var book = new TranslationBook<Dictionary<string, string>>(_ => "m", lines => lines, kept: 10);
        book.Put("o1", English);
        Assert.True(book.Add("o1", new Translated("fr", English, new Dictionary<string, string> { ["casual"] = "BUT !" }, [])));

        book.Put("o1", new Dictionary<string, string>(English)); // approved by the critic: same lines, republished

        Assert.Single(book.Current("o1")!.Value.Translations);
        Assert.Empty(book.Pending("o1", [French])!.Value.Missing);
    }

    [Fact]
    public void Only_left_to_right_languages_are_offered()
    {
        Assert.Equal(["fr", "es", "de", "pt", "it"], Languages.Supported.Select(l => l.Code));
    }

    private static StudioPipeline Pipeline(string reply)
    {
        var translator = new Translator(new ChatClientAgent(new Reply(reply)), NullLogger<Translator>.Instance);
        var team = StudioTeam.TemplatesOnly with { Translator = translator, FastModel = "gpt-4.1-mini" };
        return new StudioPipeline(new TemplateNarrator(), team, NullLogger<StudioPipeline>.Instance);
    }

    /// <summary>A model that always gives the same reply.</summary>
    private sealed class Reply(string text) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text)));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
