using System.Diagnostics;
using System.Threading.Channels;
using MatchDirector.Agents.Narration;
using MatchDirector.Contracts;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents;

/// <summary>
/// The director: runs the stats engine over each match, decides which moments go on air and turns them into overlays.
/// Narrated runs off the event path; if it falls behind, the oldest queued moments are dropped rather than shown late.
/// </summary>
public sealed class Director(INarrator narrator, OverlayPublisher publisher, ILogger<Director> logger)
{
    public static readonly ActivitySource Activity = new("MatchDirector.Agents");

    private readonly Channel<(string MatchId, Moment Moment, MomentFacts Facts)> _queue =
        Channel.CreateBounded<(string, Moment, MomentFacts)>(new BoundedChannelOptions(6) { FullMode = BoundedChannelFullMode.DropOldest });

    private MatchInfo? _info;
    private MatchAnalyzer? _analyzer;
    private long _lastSnapshot;

    public async Task StartMatchAsync(MatchInfo info, CancellationToken ct)
    {
        _info = info;
        _analyzer = new MatchAnalyzer(info.MatchId, info.Home.TeamId, info.Away.TeamId);
        logger.LogInformation("Directing {MatchId}: {Home} v {Away}", info.MatchId, info.Home.Name, info.Away.Name);
        await publisher.PublishAsync(info, ct);
    }

    public async Task IngestAsync(MatchEvent e, CancellationToken ct)
    {
        if (_analyzer is null || _info is null || e.MatchId != _info.MatchId)
            return; // joined mid-match without match info; wait for the next kick-off

        var moments = _analyzer.Ingest(e);
        var snapshot = _analyzer.Snapshot();
        foreach (var moment in moments)
            await _queue.Writer.WriteAsync((_info.MatchId, moment, MomentFacts.From(moment, _info, snapshot)), ct);

        if (moments.Count > 0 || Stopwatch.GetElapsedTime(_lastSnapshot) > TimeSpan.FromSeconds(2))
        {
            _lastSnapshot = Stopwatch.GetTimestamp();
            await publisher.PublishAsync(snapshot, ct);
        }
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
