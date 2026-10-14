using System.Diagnostics;
using System.Threading.Channels;
using MatchDirector.Agents.Narration;
using MatchDirector.Agents.Studio;
using MatchDirector.Contracts;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents;

/// <summary>
/// The director orchestrates the studio. It runs the stats engine over each match and decides what goes on air:
/// <list type="number">
/// <item>Fast lane: the narrator words each moment, code checks every number, and the overlay goes live in about two seconds.</item>
/// <item>Review lane (background): for non-routine moments the verifier critic checks the meaning, the narrator rewrites
/// anything it flags, and for important moments the tactical analyst adds why it matters. The overlay is then republished.</item>
/// </list>
/// Each lane drops its oldest work rather than falling behind the live match. Events replayed while catching up after a
/// restart (<c>live: false</c>) rebuild the stats silently, so the score is right when the director goes live mid-match.
/// </summary>
public sealed class Director(INarrator narrator, StudioTeam studio, OverlayPublisher publisher, ILogger<Director> logger)
{
    public static readonly ActivitySource Activity = new("MatchDirector.Agents");

    /// <summary>Moments at or above this priority are fact-checked by the critic (routine ones rely on the code check).</summary>
    public const int ReviewPriority = 45;

    /// <summary>Moments at or above this priority also get a tactical insight.</summary>
    public const int InsightPriority = 55;

    private const int SessionsKept = 3, RecentMoments = 10;

    private readonly Channel<OnAir> _narration = Channel.CreateBounded<OnAir>(new BoundedChannelOptions(6) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly Channel<InReview> _reviews = Channel.CreateBounded<InReview>(new BoundedChannelOptions(8) { FullMode = BoundedChannelFullMode.DropOldest });
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
            var snapshot = session.Analyzer.Snapshot();
            foreach (var moment in moments)
            {
                var facts = MomentFacts.From(moment, session.Info, snapshot);
                session.Remember(new RecentMoment(facts.Minute, moment.Type.ToString(), facts.Team, facts.Player, moment.Metrics));
                if (live)
                    await _narration.Writer.WriteAsync(new OnAir(session.Info.MatchId, moment, facts, new StudioContext(session.Info, snapshot, session.Recent.ToList())), ct);
            }
            if (!live)
                return;

            if (Stopwatch.GetElapsedTime(session.LastAnnounced) > TimeSpan.FromSeconds(30))
                await AnnounceAsync(session, ct);
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

    /// <summary>Fast lane: narrate, check numbers, publish. Runs until cancelled.</summary>
    public async Task RunNarrationAsync(CancellationToken ct)
    {
        await foreach (var item in _narration.Reader.ReadAllAsync(ct))
        {
            var (matchId, moment, facts, _) = item;
            using var activity = Activity.StartActivity("narrate-moment");
            activity?.SetTag("moment.id", moment.MomentId);
            activity?.SetTag("moment.type", moment.Type.ToString());

            var trace = new List<AgentStep>();
            var started = Stopwatch.GetTimestamp();
            var narration = await narrator.NarrateAsync(facts, ct);
            trace.Add(new("Narrator", studio.FastModel, narration.Source == "agent" ? "wrote 4 audience lines" : "template (model unavailable)", Ms(started)));
            narration = CheckNumbers(narration, facts, trace);
            activity?.SetTag("narration.source", narration.Source);

            await publisher.PublishAsync(ToOverlay(matchId, moment, narration, null, trace), ct);
            logger.LogInformation("{Minute}' {Type} [{Source}, {Ms} ms] {Casual} | {Analyst}",
                facts.Minute, moment.Type, narration.Source, Ms(started), narration.Casual, narration.Analyst);

            if (studio.HasReviewers && narration.Source == "agent" && moment.Priority >= ReviewPriority)
                _reviews.Writer.TryWrite(new InReview(item, narration, trace));
        }
    }

    /// <summary>Review lane: tactical insight, fact-check, correction, republish. Runs until cancelled.</summary>
    public async Task RunReviewsAsync(CancellationToken ct)
    {
        if (studio.Critic is not { } critic)
            return;

        await foreach (var (item, original, fastTrace) in _reviews.Reader.ReadAllAsync(ct))
        {
            var (matchId, moment, facts, context) = item;
            using var activity = Activity.StartActivity("review-moment");
            activity?.SetTag("moment.id", moment.MomentId);
            var trace = fastTrace.ToList();
            var narration = original;
            Insight? insight = null;

            if (studio.Analyst is { } analyst && moment.Priority >= InsightPriority)
            {
                var started = Stopwatch.GetTimestamp();
                if (await analyst.AnalyseAsync(facts, context, ct) is { } result)
                {
                    var invented = NarrationVerifier.UnsupportedNumbers([result.Text], facts.AllowedNumbers(result.ToolNumbers));
                    insight = invented.Count == 0 ? result : null;
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
            var review = Review.Skipped;
            for (var round = 1; round <= 2; round++)
            {
                var reviewed = Stopwatch.GetTimestamp();
                review = await critic.ReviewAsync(facts, narration, insight, ct);
                trace.Add(new(round == 1 ? "Verifier critic" : "Verifier critic (recheck)", studio.ReasoningModel,
                    review.Pass ? "approved" : $"flagged {review.Issues.Count} issue(s)", Ms(reviewed),
                    review.Pass ? null : string.Join(" · ", review.Issues)));
                if (review.Pass)
                    break;

                if (review.Issues.Any(IsAboutInsight))
                    insight = null;
                var lineIssues = review.Issues.Where(i => !IsAboutInsight(i)).ToList();
                if (lineIssues.Count == 0)
                    break;

                if (round == 2)
                {
                    narration = TemplateNarrator.Narrate(facts);
                    trace.Add(new("Director", "code", "used template after second failed review", 0));
                    break;
                }

                var revised = Stopwatch.GetTimestamp();
                narration = await narrator.ReviseAsync(facts, narration, lineIssues, ct);
                trace.Add(new("Narrator", studio.FastModel, narration.Source == "agent" ? "rewrote lines after review" : "template after failed rewrite", Ms(revised)));
                narration = CheckNumbers(narration, facts, trace);
                if (narration.Source != "agent")
                    break; // the template is deterministic; nothing for the critic to recheck
            }

            activity?.SetTag("review.pass", review.Pass);
            await publisher.PublishAsync(ToOverlay(matchId, moment, narration, insight?.Text, trace), ct);
            logger.LogInformation("{Minute}' {Type} reviewed: {Verdict}{Insight}", facts.Minute, moment.Type,
                review.Pass ? "approved" : $"corrected ({string.Join(" · ", review.Issues)})", insight is null ? "" : $" | insight: {insight.Text}");
        }
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

    private sealed record OnAir(string MatchId, Moment Moment, MomentFacts Facts, StudioContext Context);

    private sealed record InReview(OnAir Item, Narrated Narration, IReadOnlyList<AgentStep> Trace);

    private sealed class Session(MatchInfo info)
    {
        private readonly Queue<RecentMoment> _recent = new();

        public MatchInfo Info { get; } = info;
        public MatchAnalyzer Analyzer { get; } = new(info.MatchId, info.Home.TeamId, info.Away.TeamId);
        public long LastAnnounced { get; set; }
        public long LastSnapshot { get; set; }
        public IEnumerable<RecentMoment> Recent => _recent;

        public void Remember(RecentMoment moment)
        {
            _recent.Enqueue(moment);
            if (_recent.Count > RecentMoments)
                _recent.Dequeue();
        }
    }
}
