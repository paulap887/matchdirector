using MatchDirector.Contracts;

namespace MatchDirector.StatsEngine;

/// <summary>
/// Turns a live event stream into running statistics and evidence-backed moments. Deterministic: the same events
/// always produce the same numbers and moments. Feed events in order with <see cref="Ingest"/>.
/// </summary>
public sealed class MatchAnalyzer
{
    private const int EvidenceLimit = 10;

    private readonly string _matchId;
    private readonly string _homeId;
    private readonly string _awayId;
    private readonly Dictionary<string, TeamTally> _teams;
    private readonly Dictionary<string, PlayerTally> _players = [];
    private readonly MomentumMeter _momentum;
    private readonly TempoMeter _tempo = new();
    private readonly List<MatchEvent> _sequence = [];
    private readonly List<string> _goalEvents = [];

    private int _period;
    private TimeSpan _clock;
    private string? _possession;
    private int _passChain;
    private (MatchEvent Shot, ShotMetrics Metrics)? _pendingShot;
    private string? _momentumLeader;
    private double _lastMomentumShift = double.MinValue;
    private bool? _chaotic;
    private double _lastTempoShift = double.MinValue;
    private readonly Dictionary<(MomentType, string), double> _lastSeen = [];

    public MatchAnalyzer(string matchId, string homeTeamId, string awayTeamId)
    {
        _matchId = matchId;
        _homeId = homeTeamId;
        _awayId = awayTeamId;
        _teams = new() { [homeTeamId] = new(homeTeamId), [awayTeamId] = new(awayTeamId) };
        _momentum = new MomentumMeter(homeTeamId);
    }

    public IReadOnlyList<Moment> Ingest(MatchEvent e)
    {
        var moments = new List<Moment>();
        ResolvePendingShot(e, moments);
        AdvanceClock(e);

        var team = Team(e.TeamId);
        var player = e.PlayerId is null ? null : Player(e.PlayerId, e.TeamId);
        var start = Frame(e.TeamId, e.Start);
        var now = Seconds(e.MatchClock);

        switch (e.Type)
        {
            case MatchEventType.KickOff:
            case MatchEventType.PossessionChange:
                StartPossession(e);
                break;

            case MatchEventType.Pass:
                Track(e);
                team.Passes++;
                player!.Passes++;
                if (e.End is null)
                    break;
                var passEnd = Frame(e.TeamId, e.End.Value);
                var pass = PassMetrics.From(e, start, passEnd);
                if (e.Outcome != EventOutcome.Success)
                {
                    _passChain = 0;
                    break;
                }

                team.PassesCompleted++;
                player.PassesCompleted++;
                _tempo.Add(now, TempoMeter.Signal.CompletedPass, e.EventId);
                if (pass.Progressive)
                {
                    team.ProgressivePasses++;
                    player.ProgressivePasses++;
                }
                _momentum.Add(now, e.TeamId, (pass.Progressive ? 0.03 : 0) + (pass.IntoBox ? 0.04 : 0)
                                             + (Pitch.InFinalThird(passEnd) && !Pitch.InFinalThird(start) ? 0.02 : 0), e.EventId);

                if (++_passChain == 10 && Cooled(MomentType.PassingMove, e.TeamId, now))
                {
                    var chain = _sequence.Where(s => s.Type == MatchEventType.Pass).TakeLast(10).ToList();
                    moments.Add(Create(MomentType.PassingMove, e, 45, new()
                    {
                        ["passes"] = 10,
                        ["players"] = chain.Select(c => c.PlayerId).Distinct().Count(),
                        ["seconds"] = Math.Round((e.MatchClock - chain[0].MatchClock).TotalSeconds),
                        ["possession"] = Possession(e.TeamId)
                    }, chain.Select(c => c.EventId)));
                }
                if (pass.Difficulty >= 0.85 && Cooled(MomentType.DifficultPass, e.TeamId, now))
                {
                    moments.Add(Create(MomentType.DifficultPass, e, 30, new()
                    {
                        ["difficulty"] = pass.Difficulty,
                        ["distance"] = pass.Distance,
                        ["progression"] = pass.Progression,
                        ["speed"] = pass.Speed ?? 0
                    }, [e.EventId]));
                }
                break;

            case MatchEventType.Carry:
                Track(e);
                if (e.End is { } carryEnd)
                {
                    player!.DistanceCarried += Pitch.Distance(e.Start, carryEnd);
                    var carryFrame = Frame(e.TeamId, carryEnd);
                    _momentum.Add(now, e.TeamId, Pitch.InFinalThird(carryFrame) && !Pitch.InFinalThird(start) ? 0.01 : 0, e.EventId);
                }
                break;

            case MatchEventType.Shot:
                Track(e);
                var shot = ShotMetrics.From(e, start);
                team.Shots++;
                team.Xg += shot.Xg;
                player!.Shots++;
                player.Xg += shot.Xg;
                if (shot.Speed is { } speed && speed > (player.FastestShot ?? 0))
                    player.FastestShot = speed;
                _momentum.Add(now, e.TeamId, shot.Xg + 0.02, e.EventId);
                _pendingShot = (e, shot);
                break;

            case MatchEventType.Goal:
                team.Goals++;
                player!.Goals++;
                _goalEvents.Add(e.EventId);
                break;

            case MatchEventType.Save:
                break;

            case MatchEventType.Pressure:
                team.Pressures++;
                _tempo.Add(now, TempoMeter.Signal.Pressure, e.EventId);
                break;

            case MatchEventType.Tackle:
                if (e.Outcome == EventOutcome.Success)
                {
                    team.Tackles++;
                    player!.Tackles++;
                    HighRegain(e, start, moments);
                }
                break;

            case MatchEventType.Interception:
                team.Interceptions++;
                player!.Interceptions++;
                HighRegain(e, start, moments);
                break;

            case MatchEventType.Foul:
                team.Fouls++;
                break;

            case MatchEventType.PeriodEnd:
                moments.Add(PeriodSummary(e));
                break;
        }

        DetectMomentumShift(e, now, moments);
        DetectTempoShift(e, now, moments);
        return moments;
    }

    public MatchSnapshot Snapshot() => new(
        _matchId, _clock, _period, TeamSnapshot(_homeId), TeamSnapshot(_awayId),
        _momentum.Value(Seconds(_clock)), _tempo.Value(Seconds(_clock)) ?? 50,
        _players.Values.OrderBy(p => p.PlayerId).Select(p => new PlayerStats(
            p.PlayerId, p.TeamId, p.Passes, p.PassesCompleted, p.ProgressivePasses, p.Shots, p.Goals,
            Math.Round(p.Xg, 2), Math.Round(p.DistanceCarried, 1), p.Tackles, p.Interceptions, p.FastestShot)).ToList());

    private void ResolvePendingShot(MatchEvent next, List<Moment> moments)
    {
        if (_pendingShot is not var (shot, metrics))
            return;
        _pendingShot = null;

        var shooter = Team(shot.TeamId);
        var stats = new Dictionary<string, double>
        {
            ["xg"] = metrics.Xg,
            ["distance"] = metrics.Distance,
            ["angle"] = metrics.Angle,
            ["shotSpeed"] = metrics.Speed ?? 0
        };

        if (next.Type == MatchEventType.Goal)
        {
            shooter.ShotsOnTarget++;
            var build = _sequence.TakeLast(EvidenceLimit - 1).ToList();
            stats["passesInMove"] = build.Count(b => b.Type == MatchEventType.Pass && b.Outcome == EventOutcome.Success);
            stats["moveSeconds"] = Math.Round((next.MatchClock - build[0].MatchClock).TotalSeconds);
            stats["homeScore"] = Team(_homeId).Goals + (shot.TeamId == _homeId ? 1 : 0);
            stats["awayScore"] = Team(_awayId).Goals + (shot.TeamId == _awayId ? 1 : 0);
            moments.Add(Create(MomentType.Goal, next, 100, stats, build.Select(b => b.EventId).Append(next.EventId)));
        }
        else if (next.Type == MatchEventType.Save)
        {
            shooter.ShotsOnTarget++;
            if (metrics.Xg >= 0.15)
                moments.Add(Create(MomentType.KeySave, next, 60, stats, [shot.EventId, next.EventId]));
        }
        else if (metrics.Xg >= 0.3)
        {
            moments.Add(Create(MomentType.BigChanceMissed, shot, 70, stats, [shot.EventId]));
        }
    }

    private void HighRegain(MatchEvent e, PitchPoint start, List<Moment> moments)
    {
        if (start.X < 80 || !Cooled(MomentType.HighRegain, e.TeamId, Seconds(e.MatchClock)))
            return;
        var lostBall = _sequence.LastOrDefault(); // the opponent's last action, which this regain ended
        moments.Add(Create(MomentType.HighRegain, e, 35, new() { ["distanceToGoal"] = Math.Round(Pitch.DistanceToGoal(start), 1) },
            lostBall is null ? [e.EventId] : [lostBall.EventId, e.EventId]));
    }

    private Moment PeriodSummary(MatchEvent e)
    {
        var home = TeamSnapshot(_homeId);
        var away = TeamSnapshot(_awayId);
        return Create(e.Period >= 2 ? MomentType.FullTime : MomentType.HalfTime, e with { TeamId = _homeId, PlayerId = null },
            e.Period >= 2 ? 90 : 80, new()
            {
                ["homeGoals"] = home.Goals, ["awayGoals"] = away.Goals,
                ["homeXg"] = home.Xg, ["awayXg"] = away.Xg,
                ["homeShots"] = home.Shots, ["awayShots"] = away.Shots,
                ["homePossession"] = home.Possession, ["awayPossession"] = away.Possession,
                ["homePassAccuracy"] = home.PassAccuracy, ["awayPassAccuracy"] = away.PassAccuracy
            }, _goalEvents.Append(e.EventId));
    }

    private void DetectMomentumShift(MatchEvent e, double now, List<Moment> moments)
    {
        var value = _momentum.Value(now);
        string? leader = value >= 40 ? _homeId : value <= -40 ? _awayId : null;
        if (leader is null || leader == _momentumLeader)
            return;

        var previous = _momentumLeader;
        _momentumLeader = leader;
        if (previous is null || now - _lastMomentumShift < 300)
            return;

        _lastMomentumShift = now;
        moments.Add(Create(MomentType.MomentumShift, e with { TeamId = leader, PlayerId = null }, 55,
            new() { ["momentum"] = value }, _momentum.TopContributors(leader, now, 8)));
    }

    private void DetectTempoShift(MatchEvent e, double now, List<Moment> moments)
    {
        if (_tempo.Value(now) is not { } chaos)
            return;

        bool? state = chaos >= 65 ? true : chaos <= 35 ? false : _chaotic;
        if (state is null || state == _chaotic)
            return;

        var previous = _chaotic;
        _chaotic = state;
        if (previous is null || now - _lastTempoShift < 480)
            return;

        _lastTempoShift = now;
        moments.Add(Create(MomentType.TempoShift, e with { TeamId = _homeId, PlayerId = null }, 40,
            new() { ["chaos"] = chaos }, _tempo.Evidence(EvidenceLimit)));
    }

    /// <summary>Limits routine moment types to one per team every six minutes, so the director gets highlights, not a firehose.</summary>
    private bool Cooled(MomentType type, string teamId, double now)
    {
        if (_lastSeen.TryGetValue((type, teamId), out var last) && now - last < 360 && now >= last)
            return false;
        _lastSeen[(type, teamId)] = now;
        return true;
    }

    private void AdvanceClock(MatchEvent e)
    {
        if (e.Period != _period)
        {
            _period = e.Period;
            _momentum.Reset();
            _tempo.Reset(Seconds(e.MatchClock));
            _momentumLeader = null;
            _chaotic = null;
        }
        else if (_possession is not null && e.MatchClock > _clock)
        {
            Team(_possession).PossessionTime += e.MatchClock - _clock;
        }
        _clock = e.MatchClock;
    }

    private void StartPossession(MatchEvent e)
    {
        if (e.Type == MatchEventType.PossessionChange)
            _tempo.Add(Seconds(e.MatchClock), TempoMeter.Signal.Turnover, e.EventId);
        _tempo.Add(Seconds(e.MatchClock), TempoMeter.Signal.PossessionStart, e.EventId);
        _possession = e.TeamId;
        _passChain = 0;
        _sequence.Clear();
        _sequence.Add(e);
    }

    private void Track(MatchEvent e)
    {
        if (e.TeamId != _possession)
            StartPossession(e with { Type = MatchEventType.KickOff });
        _sequence.Add(e);
    }

    private TeamStats TeamSnapshot(string teamId)
    {
        var t = Team(teamId);
        var total = _teams.Values.Sum(x => x.PossessionTime.TotalSeconds);
        return new TeamStats(
            t.TeamId, t.Goals, t.Shots, t.ShotsOnTarget, Math.Round(t.Xg, 2), t.Passes, t.PassesCompleted,
            t.Passes == 0 ? 0 : Math.Round(100.0 * t.PassesCompleted / t.Passes, 1),
            total == 0 ? 50 : Math.Round(100 * t.PossessionTime.TotalSeconds / total, 1),
            t.ProgressivePasses, t.Tackles, t.Interceptions, t.Pressures, t.Fouls);
    }

    private double Possession(string teamId) => TeamSnapshot(teamId).Possession;

    private Moment Create(MomentType type, MatchEvent at, int priority, Dictionary<string, double> metrics, IEnumerable<string> evidence) =>
        new($"{at.EventId}:{type}", type, at.MatchClock, at.Period, at.TeamId, at.PlayerId, priority, metrics, evidence.Distinct().ToList());

    private PitchPoint Frame(string teamId, PitchPoint p) => Pitch.ToAttackingFrame(p, teamId == _homeId, Math.Max(_period, 1));
    private TeamTally Team(string teamId) => _teams[teamId];
    private PlayerTally Player(string playerId, string teamId) =>
        _players.TryGetValue(playerId, out var p) ? p : _players[playerId] = new PlayerTally(playerId, teamId);
    private static double Seconds(TimeSpan t) => t.TotalSeconds;

    private sealed class TeamTally(string teamId)
    {
        public string TeamId { get; } = teamId;
        public int Goals, Shots, ShotsOnTarget, Passes, PassesCompleted, ProgressivePasses, Tackles, Interceptions, Pressures, Fouls;
        public double Xg;
        public TimeSpan PossessionTime;
    }

    private sealed class PlayerTally(string playerId, string teamId)
    {
        public string PlayerId { get; } = playerId;
        public string TeamId { get; } = teamId;
        public int Passes, PassesCompleted, ProgressivePasses, Shots, Goals, Tackles, Interceptions;
        public double Xg, DistanceCarried;
        public double? FastestShot;
    }
}
