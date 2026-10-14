using System.Diagnostics;
using System.Threading.Channels;
using MatchDirector.Agents.Narration;
using MatchDirector.Contracts;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents;

/// <summary>
/// The director: runs the stats engine over each match, decides which moments go on air and turns them into overlays.
/// Narration runs off the event path; if it falls behind, the oldest queued moments are dropped rather than shown late.
/// Events replayed while catching up after a restart (<c>live: false</c>) rebuild the stats silently, so the score is
/// right when the director goes live mid-match.
/// </summary>
public sealed class Director(INarrator narrator, OverlayPublisher publisher, ILogger<Director> logger)
{
    public static readonly ActivitySource Activity = new("MatchDirector.Agents");
    private const int SessionsKept = 3;

    private readonly Channel<(string MatchId, Moment Moment, MomentFacts Facts)> _queue =
        Channel.CreateBounded<(string, Moment, MomentFacts)>(new BoundedChannelOptions(6) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly SemaphoreSlim _gate = new(1, 1); // partitions are read concurrently
    private readonly List<Session> _sessions = [];

    public async Task StartMatchAsync(MatchInfo info, bool live, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _sessions.RemoveAll(s => s.Info.MatchId == info.MatchId);
            _sessions.Add(new Session(info));
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

            var moments = session.Analyzer.Ingest(e);
            if (!live)
                return;
            if (Stopwatch.GetElapsedTime(session.LastAnnounced) > TimeSpan.FromSeconds(30))
                await AnnounceAsync(session, ct);

            var snapshot = session.Analyzer.Snapshot();
            foreach (var moment in moments)
                await _queue.Writer.WriteAsync((session.Info.MatchId, moment, MomentFacts.From(moment, session.Info, snapshot)), ct);

            if (moments.Count > 0 || Stopwatch.GetElapsedTime(session.LastSnapshot) > TimeSpan.FromSeconds(2))
            {
                session.LastSnapshot = Stopwatch.GetTimestamp();
                await publisher.PublishAsync(snapshot, ct);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Tells the hub which match is on. Repeated every 30 seconds while live, so a hub that restarted
    /// (and lost its in-memory state) recovers on its own.
    /// </summary>
    private async Task AnnounceAsync(Session session, CancellationToken ct)
    {
        session.LastAnnounced = Stopwatch.GetTimestamp();
        await publisher.PublishAsync(session.Info, ct);
        await publisher.PublishAsync(session.Analyzer.Snapshot(), ct);
    }

    private sealed class Session(MatchInfo info)
    {
        public MatchInfo Info { get; } = info;
        public MatchAnalyzer Analyzer { get; } = new(info.MatchId, info.Home.TeamId, info.Away.TeamId);
        public long LastAnnounced { get; set; }
        public long LastSnapshot { get; set; }
    }

    /// <summary>Narrates queued moments and publishes them as overlays until cancelled.</summary>
    public async Task RunNarrationAsync(CancellationToken ct)
    {
        await foreach (var (matchId, moment, facts) in _queue.Reader.ReadAllAsync(ct))
        {
            using var activity = Activity.StartActivity("narrate-moment");
            activity?.SetTag("moment.id", moment.MomentId);
            activity?.SetTag("moment.type", moment.Type.ToString());

            var started = Stopwatch.GetTimestamp();
            var narration = await narrator.NarrateAsync(facts, ct);
            var unsupported = NarrationVerifier.UnsupportedNumbers(narration, facts);
            if (unsupported.Count > 0)
            {
                logger.LogWarning("Verifier rejected {Source} narration for {MomentId}: unsupported numbers {Numbers}",
                    narration.Source, moment.MomentId, unsupported);
                narration = TemplateNarrator.Narrate(facts);
            }
            activity?.SetTag("narration.source", narration.Source);

            await publisher.PublishAsync(ToOverlay(matchId, moment, narration), ct);
            logger.LogInformation("{Clock:mm\\:ss} {Type} [{Source}, {Ms} ms] {Casual} | {Analyst}",
                moment.MatchClock, moment.Type, narration.Source, (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                narration.Casual, narration.Analyst);
        }
    }

    private static Overlay ToOverlay(string matchId, Moment moment, Narrated narration) => new(
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
        new Dictionary<string, string>
        {
            ["casual"] = narration.Casual,
            ["analyst"] = narration.Analyst,
            ["homeFan"] = narration.HomeFan,
            ["awayFan"] = narration.AwayFan,
            ["source"] = narration.Source
        },
        moment.Participants);
}
