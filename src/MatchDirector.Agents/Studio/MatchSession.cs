using MatchDirector.Agents.Narration;
using MatchDirector.Contracts;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents.Studio;

/// <summary>One match being directed: its stats engine and the recent moments the tactical analyst can look up.</summary>
public sealed class MatchSession(MatchInfo info)
{
    private const int RecentMoments = 10;
    private readonly Queue<RecentMoment> _recent = new();

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
            result.Add((moment, facts, new StudioContext(Info, snapshot, _recent.ToList())));
        }
        return result;
    }
}
