using MatchDirector.Agents.Narration;
using MatchDirector.Contracts;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents.Studio;

/// <summary>One match being directed: its stats engine and the recent moments the tactical analyst can look up.</summary>
public sealed class MatchSession(MatchInfo info)
{
    private const int RecentMoments = 10, KeyMomentsKept = 12;
    private readonly Queue<RecentMoment> _recent = new();
    private readonly List<Recap.KeyMoment> _keyMoments = [];

    public MatchInfo Info { get; } = info;
    public MatchAnalyzer Analyzer { get; } = new(info.MatchId, info.Home.TeamId, info.Away.TeamId);
    public long LastAnnounced { get; set; }
    public long LastSnapshot { get; set; }

    /// <summary>Feeds one event to the stats engine and returns each new moment with its facts and analyst context.</summary>
    public IReadOnlyList<(Moment Moment, MomentFacts Facts, StudioContext Context)> Ingest(MatchEvent e)
    {
        var moments = Analyzer.Ingest(e);
        if (moments.Count == 0)
            return [];

        var snapshot = Analyzer.Snapshot();
        var result = new List<(Moment, MomentFacts, StudioContext)>();
        foreach (var moment in moments)
        {
            var facts = MomentFacts.From(moment, Info, snapshot);
            _recent.Enqueue(new RecentMoment(facts.Minute, moment.Type.ToString(), facts.Team, facts.Player, moment.Metrics));
            if (_recent.Count > RecentMoments)
                _recent.Dequeue();
            if (moment.Priority >= StudioPipeline.InsightPriority && moment.Type is not (MomentType.HalfTime or MomentType.FullTime))
                _keyMoments.Add(new Recap.KeyMoment(facts.Minute, moment.Type.ToString(), facts.Team, facts.Player, moment.Metrics, moment.Evidence));
            result.Add((moment, facts, new StudioContext(Info, snapshot, _recent.ToList())));
        }
        return result;
    }

    /// <summary>
    /// Facts for the full-time report: the final stats and the key moments. Goals are always kept; other key moments
    /// fill the remaining places, most recent last.
    /// </summary>
    public Recap.RecapFacts RecapFacts()
    {
        var goals = _keyMoments.Where(m => m.Type == nameof(MomentType.Goal)).ToList();
        var others = _keyMoments.Except(goals).TakeLast(Math.Max(0, KeyMomentsKept - goals.Count));
        var keep = goals.Concat(others).OrderBy(m => m.Minute).ToList();
        return Recap.RecapFacts.From(Info, Analyzer.Snapshot(), keep);
    }
}
