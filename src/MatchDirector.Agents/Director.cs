using System.Diagnostics;
using System.Threading.Channels;
using MatchDirector.Agents.Narration;
using MatchDirector.Agents.Studio;
using MatchDirector.Agents.Translation;
using MatchDirector.Contracts;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents;

/// <summary>
/// The director orchestrates the live studio. It runs a <see cref="MatchSession"/> per match and decides what goes on air:
/// <list type="number">
/// <item>Fast lane: <see cref="StudioPipeline.AirAsync"/> narrates and checks numbers; the overlay is live in about two seconds.</item>
/// <item>Review lane (background): <see cref="StudioPipeline.ReviewAsync"/> fact-checks, corrects and adds insight; the
/// overlay is then republished with the same ID.</item>
/// <item>Translation lane (background): <see cref="StudioPipeline.TranslateAsync"/> translates what is on air into the
/// languages viewers are watching in, and only those; the overlay is republished with the translations added.</item>
/// </list>
/// Each lane drops its oldest work rather than falling behind the live match. Events replayed while catching up after a
/// restart (<c>live: false</c>) rebuild the stats silently, so the score is right when the director goes live mid-match.
/// </summary>
public sealed class Director(StudioPipeline pipeline, OverlayPublisher publisher, ILogger<Director> logger)
{
    private const int SessionsKept = 3;
    private const int CatchUpOverlays = 12; // a viewer who picks a language sees the recent feed translated too

    private static readonly string[] TextLines = ["casual", "analyst", "homeFan", "awayFan", "insight"];

    private readonly Channel<OnAir> _narration = Channel.CreateBounded<OnAir>(new BoundedChannelOptions(6) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<InReview> _reviews = Channel.CreateBounded<InReview>(new BoundedChannelOptions(8) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<Recap.RecapFacts> _recaps = Channel.CreateBounded<Recap.RecapFacts>(new BoundedChannelOptions(2) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<Translate> _translations = Channel.CreateBounded<Translate>(new BoundedChannelOptions(32) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly SemaphoreSlim _gate = new(1, 1); // partitions are read concurrently
    private readonly SemaphoreSlim _publishing = new(1, 1); // lanes republish the same overlay; the hub must get the latest last
    private readonly List<MatchSession> _sessions = [];
    private readonly TranslationBook<Overlay> _overlayBook = new(o => o.MatchId, EnglishOf, 60);
    private readonly TranslationBook<MatchRecap> _recapBook = new(r => r.MatchId, EnglishOf, SessionsKept);
    private volatile IReadOnlyList<Language> _languages = [];
    private volatile string? _onAir;

    public async Task StartMatchAsync(MatchInfo info, bool live, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _sessions.RemoveAll(s => s.Info.MatchId == info.MatchId);
            _sessions.Add(new MatchSession(info));
            if (_sessions.Count > SessionsKept)
                _sessions.RemoveAt(0);
            _onAir = info.MatchId;
            logger.LogInformation("Directing {MatchId}: {Home} v {Away}{Mode}", info.MatchId, info.Home.Name, info.Away.Name, live ? "" : " (catching up)");
            if (live)
                await AnnounceAsync(_sessions[^1], ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task IngestAsync(MatchEvent e, bool live, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_sessions.Find(s => s.Info.MatchId == e.MatchId) is not { } session)
                return; // its match info is older than the catch-up window

            var moments = session.Ingest(e);
            if (!live)
                return;

            foreach (var (moment, facts, context) in moments)
            {
                await _narration.Writer.WriteAsync(new OnAir(session.Info.MatchId, moment, facts, context), ct);
                if (moment.Type == MomentType.FullTime)
                    _recaps.Writer.TryWrite(session.RecapFacts());
            }

            if (Stopwatch.GetElapsedTime(session.LastAnnounced) > TimeSpan.FromSeconds(30))
                await AnnounceAsync(session, ct);
            if (moments.Count > 0 || Stopwatch.GetElapsedTime(session.LastSnapshot) > TimeSpan.FromSeconds(2))
            {
                session.LastSnapshot = Stopwatch.GetTimestamp();
                await publisher.PublishAsync(session.Analyzer.Snapshot(), ct);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Fast lane: narrate, check numbers, publish. Runs until cancelled.</summary>
    public async Task RunNarrationAsync(CancellationToken ct)
    {
        await foreach (var item in _narration.Reader.ReadAllAsync(ct))
        {
            var started = Stopwatch.GetTimestamp();
            var aired = await pipeline.AirAsync(item.MatchId, item.Moment, item.Facts, ct);
            await PublishAsync(aired.Overlay, ct);
            logger.LogInformation("{Minute}' {Type} [{Source}, {Ms} ms] {Casual} | {Analyst}", item.Facts.Minute, item.Moment.Type,
                aired.Narration.Source, (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds, aired.Narration.Casual, aired.Narration.Analyst);

            if (pipeline.ShouldReview(item.Moment, aired))
                _reviews.Writer.TryWrite(new InReview(item, aired));
        }
    }

    /// <summary>Review lane: insight, fact-check, correction, republish. Runs until cancelled.</summary>
    public async Task RunReviewsAsync(CancellationToken ct)
    {
        if (!pipeline.Studio.HasReviewers)
            return;

        await foreach (var (item, aired) in _reviews.Reader.ReadAllAsync(ct))
        {
            var reviewed = await pipeline.ReviewAsync(item.MatchId, item.Moment, item.Facts, item.Context, aired, ct);
            await PublishAsync(reviewed.Overlay, ct);
            logger.LogInformation("{Minute}' {Type} reviewed: {Verdict}{Insight}", item.Facts.Minute, item.Moment.Type,
                reviewed.FirstReview.Pass ? "approved" : $"corrected ({string.Join(" · ", reviewed.FirstReview.Issues)})",
                reviewed.Insight is null ? "" : $" | insight: {reviewed.Insight.Text}");
        }
    }

    /// <summary>Full-time reports: written, checked and published as each match ends. Runs until cancelled.</summary>
    public async Task RunRecapsAsync(CancellationToken ct)
    {
        await foreach (var facts in _recaps.Reader.ReadAllAsync(ct))
        {
            var recap = await pipeline.RecapAsync(facts, ct);
            _recapBook.Put(recap.MatchId, recap);
            await PublishCurrentAsync(recap.MatchId, recap: true, ct);
            if (_languages.Count > 0)
                _translations.Writer.TryWrite(new Translate(recap.MatchId, Recap: true));
            logger.LogInformation("Recap {MatchId} [{Source}]: {Headline}", recap.MatchId, recap.Variants["source"], recap.Headline);
        }
    }

    /// <summary>
    /// Translation lane: follows which languages viewers are watching in (reported by the hub) and translates overlays
    /// and full-time reports into those languages only. Runs until cancelled.
    /// </summary>
    public async Task RunTranslationsAsync(CancellationToken ct)
    {
        if (pipeline.Studio.Translator is null)
            return;
        await Task.WhenAll(WatchLanguagesAsync(ct), TranslateAsync(ct));
    }

    private async Task WatchLanguagesAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        do
        {
            var watched = (await publisher.LanguagesAsync(ct)).Select(Languages.Find).OfType<Language>().ToList();
            var added = watched.Except(_languages).ToList();
            if (added.Count > 0 || watched.Count != _languages.Count)
                logger.LogInformation("Viewers watching in: {Languages}", watched.Count == 0 ? "English only" : string.Join(", ", watched.Select(l => l.Code)));
            _languages = watched;

            // A newly chosen language catches up on the full-time report and the recent moments, newest first.
            if (added.Count > 0 && _onAir is { } matchId)
            {
                _translations.Writer.TryWrite(new Translate(matchId, Recap: true));
                foreach (var id in _overlayBook.Ids(matchId).Take(CatchUpOverlays))
                    _translations.Writer.TryWrite(new Translate(id, Recap: false));
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task TranslateAsync(CancellationToken ct)
    {
        await foreach (var (id, recap) in _translations.Reader.ReadAllAsync(ct))
        {
            var languages = _languages;
            var translated = recap ? await TranslateAsync(_recapBook, id, languages, ct) : await TranslateAsync(_overlayBook, id, languages, ct);
            if (translated.Count > 0)
            {
                await PublishCurrentAsync(id, recap, ct);
                logger.LogInformation("Translated {Id} into {Languages}", id, string.Join(", ", translated));
            }
        }
    }

    /// <summary>Translates an item into the languages it still needs, in parallel; returns the languages stored.</summary>
    private async Task<IReadOnlyList<string>> TranslateAsync<T>(TranslationBook<T> book, string id, IReadOnlyList<Language> languages, CancellationToken ct)
    {
        if (languages.Count == 0 || book.Pending(id, languages) is not { } pending || pending.Missing.Count == 0)
            return [];
        var translations = await Task.WhenAll(pending.Missing.Select(l => pipeline.TranslateAsync(l, pending.English, ct)));
        // A translation of lines the review lane has since replaced is dropped; the new version is already queued.
        return translations.Where(t => book.Add(id, t)).Select(t => t.Language).ToList();
    }

    private async Task PublishAsync(Overlay overlay, CancellationToken ct)
    {
        _overlayBook.Put(overlay.OverlayId, overlay);
        await PublishCurrentAsync(overlay.OverlayId, recap: false, ct);
        if (_languages.Count > 0)
            _translations.Writer.TryWrite(new Translate(overlay.OverlayId, Recap: false));
    }

    /// <summary>Publishes the latest version of an item with the translations that still match its English lines.</summary>
    private async Task PublishCurrentAsync(string id, bool recap, CancellationToken ct)
    {
        await _publishing.WaitAsync(ct);
        try
        {
            if (recap && _recapBook.Current(id) is { } r)
                await publisher.PublishAsync(r.Item with { Variants = Merge(r.Item.Variants, r.Translations), Trace = WithSteps(r.Item.Trace, r.Translations) }, ct);
            else if (!recap && _overlayBook.Current(id) is { } o)
                await publisher.PublishAsync(o.Item with { Variants = Merge(o.Item.Variants, o.Translations), Trace = WithSteps(o.Item.Trace, o.Translations) }, ct);
        }
        finally
        {
            _publishing.Release();
        }
    }

    /// <summary>Translated lines go in the same variants under "language:key", for example "fr:casual".</summary>
    private static Dictionary<string, string> Merge(IReadOnlyDictionary<string, string> variants, IEnumerable<Translated> translations)
    {
        var merged = variants.ToDictionary();
        foreach (var translation in translations)
            foreach (var (key, line) in translation.Lines)
                merged[$"{translation.Language}:{key}"] = line;
        return merged;
    }

    private static List<AgentStep> WithSteps(IReadOnlyList<AgentStep>? trace, IEnumerable<Translated> translations) =>
        [.. trace ?? [], .. translations.SelectMany(t => t.Trace)];

    /// <summary>The English lines a viewer reads, which are what gets translated.</summary>
    private static IReadOnlyDictionary<string, string> EnglishOf(Overlay overlay) =>
        TextLines.Where(overlay.Variants.ContainsKey).ToDictionary(k => k, k => overlay.Variants[k]);

    private static IReadOnlyDictionary<string, string> EnglishOf(MatchRecap recap)
    {
        var lines = TextLines.Where(recap.Variants.ContainsKey).ToDictionary(k => k, k => recap.Variants[k]);
        lines["headline"] = recap.Headline;
        return lines;
    }

    /// <summary>
    /// Tells the hub which match is on. Repeated every 30 seconds while live, so a hub that restarted
    /// (and lost its in-memory state) recovers on its own.
    /// </summary>
    private async Task AnnounceAsync(MatchSession session, CancellationToken ct)
    {
        session.LastAnnounced = Stopwatch.GetTimestamp();
        await publisher.PublishAsync(session.Info, ct);
        await publisher.PublishAsync(session.Analyzer.Snapshot(), ct);
    }

    private sealed record OnAir(string MatchId, Moment Moment, MomentFacts Facts, StudioContext Context);

    private sealed record InReview(OnAir Item, Aired Aired);

    private sealed record Translate(string Id, bool Recap);
}
