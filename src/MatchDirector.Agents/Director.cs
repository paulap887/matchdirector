using System.Diagnostics;
using System.Threading.Channels;
using MatchDirector.Agents.Narration;
using MatchDirector.Agents.Studio;
using MatchDirector.Contracts;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents;

/// <summary>
/// The director orchestrates the live studio. It runs a <see cref="MatchSession"/> per match and decides what goes on air:
/// <list type="number">
/// <item>Fast lane: <see cref="StudioPipeline.AirAsync"/> narrates and checks numbers; the overlay is live in about two seconds.</item>
/// <item>Review lane (background): <see cref="StudioPipeline.ReviewAsync"/> fact-checks, corrects and adds insight; the
/// overlay is then republished with the same ID.</item>
/// </list>
/// Each lane drops its oldest work rather than falling behind the live match. Events replayed while catching up after a
/// restart (<c>live: false</c>) rebuild the stats silently, so the score is right when the director goes live mid-match.
/// </summary>
public sealed class Director(StudioPipeline pipeline, OverlayPublisher publisher, ILogger<Director> logger)
{
    private const int SessionsKept = 3;

    private readonly Channel<OnAir> _narration = Channel.CreateBounded<OnAir>(new BoundedChannelOptions(6) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<InReview> _reviews = Channel.CreateBounded<InReview>(new BoundedChannelOptions(8) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<Recap.RecapFacts> _recaps = Channel.CreateBounded<Recap.RecapFacts>(new BoundedChannelOptions(2) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly SemaphoreSlim _gate = new(1, 1); // partitions are read concurrently
    private readonly List<MatchSession> _sessions = [];

    public async Task StartMatchAsync(MatchInfo info, bool live, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _sessions.RemoveAll(s => s.Info.MatchId == info.MatchId);
            _sessions.Add(new MatchSession(info));
            if (_sessions.Count > SessionsKept)
                _sessions.RemoveAt(0);
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
            await publisher.PublishAsync(aired.Overlay, ct);
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
            await publisher.PublishAsync(reviewed.Overlay, ct);
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
            await publisher.PublishAsync(recap, ct);
            logger.LogInformation("Recap {MatchId} [{Source}]: {Headline}", recap.MatchId, recap.Variants["source"], recap.Headline);
        }
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
}
