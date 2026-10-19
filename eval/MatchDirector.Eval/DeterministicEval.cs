using System.Diagnostics;
using MatchDirector.Agents.Narration;
using MatchDirector.Contracts;
using MatchDirector.Simulator.Engine;
using MatchDirector.StatsEngine;

namespace MatchDirector.Eval;

/// <summary>Checks that need no model: evidence integrity, template safety and stats-engine throughput.</summary>
public sealed record DeterministicResult(
    int Matches, int Events, int Moments, int MomentsWithValidEvidence, int TemplateNarrations, int TemplatesPassingNumberCheck,
    int Goals, int GoalMoments, double EventsPerSecond, double MeanGoals, double MeanShots, double MeanPassAccuracy,
    IReadOnlyDictionary<string, double> MomentsPerMatch, int Recaps, int RecapsPassingNumberCheck);

public static class DeterministicEval
{
    public static DeterministicResult Run(int matches)
    {
        int events = 0, moments = 0, valid = 0, templates = 0, templatesOk = 0, goals = 0, goalMoments = 0, recapsOk = 0;
        double shots = 0, accuracy = 0;
        var byType = new Dictionary<string, int>();
        var analysing = new Stopwatch();

        for (var seed = 1; seed <= matches; seed++)
        {
            var match = new MatchEngine(seed, Clubs.KestrelBay, Clubs.RedmoorRovers).Play();
            var analyzer = new MatchAnalyzer(match.MatchId, match.Home.Id, match.Away.Id);
            var sequenceById = match.Events.ToDictionary(e => e.EventId, e => e.Sequence);
            events += match.Events.Count;
            goals += match.Events.Count(e => e.Type == MatchEventType.Goal);

            foreach (var e in match.Events)
            {
                analysing.Start();
                var found = analyzer.Ingest(e);
                analysing.Stop();

                foreach (var moment in found)
                {
                    moments++;
                    byType[moment.Type.ToString()] = byType.GetValueOrDefault(moment.Type.ToString()) + 1;
                    if (moment.Type == MomentType.Goal)
                        goalMoments++;
                    if (moment.Evidence.Count > 0 && moment.Evidence.All(id => sequenceById.TryGetValue(id, out var s) && s <= e.Sequence))
                        valid++;

                    var facts = MomentFacts.From(moment, match.Info, analyzer.Snapshot());
                    templates++;
                    if (NarrationVerifier.UnsupportedNumbers(TemplateNarrator.Narrate(facts), facts).Count == 0)
                        templatesOk++;
                }
            }

            // Full-time report from the same session logic the director uses.
            var session = new MatchDirector.Agents.Studio.MatchSession(match.Info);
            foreach (var e in match.Events)
                session.Ingest(e);
            var recapFacts = session.RecapFacts();
            if (NarrationVerifier.UnsupportedNumbers(MatchDirector.Agents.Recap.TemplateRecap.Write(recapFacts).All, recapFacts.AllowedNumbers()).Count == 0)
                recapsOk++;

            var final = analyzer.Snapshot();
            shots += final.Home.Shots + final.Away.Shots;
            accuracy += (final.Home.PassAccuracy + final.Away.PassAccuracy) / 2;
        }

        return new DeterministicResult(
            matches, events, moments, valid, templates, templatesOk, goals, goalMoments,
            Math.Round(events / analysing.Elapsed.TotalSeconds),
            Math.Round(goals / (double)matches, 2), Math.Round(shots / matches, 1), Math.Round(accuracy / matches, 1),
            byType.OrderByDescending(t => t.Value).ToDictionary(t => t.Key, t => Math.Round(t.Value / (double)matches, 1)),
            matches, recapsOk);
    }
}
