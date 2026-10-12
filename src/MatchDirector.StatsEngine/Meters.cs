namespace MatchDirector.StatsEngine;

/// <summary>
/// Momentum from -100 (away on top) to +100 (home on top): threat each side created, decayed with a three-minute time constant.
/// </summary>
internal sealed class MomentumMeter(string homeTeamId)
{
    private const double TimeConstant = 180, Horizon = 600;
    private readonly List<(double At, string TeamId, double Threat, string EventId)> _threats = [];

    public void Reset() => _threats.Clear();

    public void Add(double at, string teamId, double threat, string eventId)
    {
        if (threat > 0)
            _threats.Add((at, teamId, threat, eventId));
        _threats.RemoveAll(t => at - t.At > Horizon);
    }

    public double Value(double now)
    {
        double home = 0, away = 0;
        foreach (var t in _threats)
        {
            var weight = t.Threat * Math.Exp(-(now - t.At) / TimeConstant);
            if (t.TeamId == homeTeamId) home += weight; else away += weight;
        }
        return Math.Round(100 * (home - away) / (home + away + 0.25), 1);
    }

    /// <summary>The events that contributed most to a team's recent threat, in match order.</summary>
    public IReadOnlyList<string> TopContributors(string teamId, double now, int count) =>
        _threats.Where(t => t.TeamId == teamId)
            .OrderByDescending(t => t.Threat * Math.Exp(-(now - t.At) / TimeConstant))
            .Take(count)
            .OrderBy(t => t.At)
            .Select(t => t.EventId)
            .ToList();
}

/// <summary>
/// Control vs chaos from 0 (calm, controlled possession) to 100 (end-to-end chaos), over a rolling five-minute window:
/// turnovers per minute, pressing actions per minute and how short possessions are.
/// </summary>
internal sealed class TempoMeter
{
    public enum Signal { Turnover, Pressure, CompletedPass, PossessionStart }

    private const double Window = 300, Warmup = 120;
    private readonly List<(double At, Signal Kind, string EventId)> _signals = [];
    private double _periodStart;

    public void Reset(double periodStart)
    {
        _signals.Clear();
        _periodStart = periodStart;
    }

    public void Add(double at, Signal kind, string eventId)
    {
        _signals.Add((at, kind, eventId));
        _signals.RemoveAll(s => at - s.At > Window);
    }

    /// <summary>Null until two minutes of the period have been played.</summary>
    public double? Value(double now)
    {
        var span = Math.Min(Window, now - _periodStart);
        if (span < Warmup)
            return null;

        var minutes = span / 60;
        var turnovers = _signals.Count(s => s.Kind == Signal.Turnover) / minutes;
        var pressures = _signals.Count(s => s.Kind == Signal.Pressure) / minutes;
        var passesPerPossession = _signals.Count(s => s.Kind == Signal.CompletedPass)
                                  / (double)Math.Max(1, _signals.Count(s => s.Kind == Signal.PossessionStart));

        var chaos = 0.4 * Math.Min(turnovers / 4, 1)
                  + 0.3 * Math.Min(pressures / 8, 1)
                  + 0.3 * (1 - Math.Min(passesPerPossession / 8, 1));
        return Math.Round(100 * chaos, 1);
    }

    public IReadOnlyList<string> Evidence(int count) =>
        _signals.Where(s => s.Kind is Signal.Turnover or Signal.Pressure).TakeLast(count).Select(s => s.EventId).ToList();
}
