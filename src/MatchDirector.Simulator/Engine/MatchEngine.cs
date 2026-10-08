using MatchDirector.Contracts;

namespace MatchDirector.Simulator.Engine;

public sealed record SimulatedMatch(string MatchId, int Seed, Club Home, Club Away, IReadOnlyList<MatchEvent> Events);

/// <summary>
/// Deterministic possession-based match simulator. The same seed always produces the same match.
/// Internally the ball is held in the possessing team's attacking frame (attacking towards x = 105)
/// and converted to fixed pitch coordinates when an event is emitted.
/// </summary>
public sealed class MatchEngine
{
    private const double Length = 105, Width = 68;
    private static readonly PitchPoint Goal = new(Length, Width / 2);
    private static readonly PitchPoint Centre = new(Length / 2, Width / 2);

    private readonly Random _rng;
    private readonly string _matchId;
    private readonly Club[] _clubs;
    private readonly List<MatchEvent> _events = [];

    private TimeSpan _clock;
    private int _period;
    private int _poss;
    private Player _carrier = null!;
    private PitchPoint _ball;

    public MatchEngine(int seed, Club home, Club away, string? matchId = null)
    {
        _rng = new Random(seed);
        _clubs = [home, away];
        _matchId = matchId ?? $"md-{seed}";
        Seed = seed;
    }

    public int Seed { get; }

    public SimulatedMatch Play()
    {
        for (_period = 1; _period <= 2; _period++)
        {
            _clock = TimeSpan.FromMinutes(_period == 1 ? 0 : 45);
            var end = TimeSpan.FromMinutes(_period * 45) + TimeSpan.FromSeconds(Between(60, _period == 1 ? 180 : 330));
            KickOff(_period == 1 ? 0 : 1);
            while (_clock < end)
                Step();
            Emit(MatchEventType.PeriodEnd, _poss, null, _ball);
        }

        return new SimulatedMatch(_matchId, Seed, _clubs[0], _clubs[1], _events);
    }

    private void KickOff(int team)
    {
        _poss = team;
        _ball = Centre;
        _carrier = _clubs[team].Striker;
        Emit(MatchEventType.KickOff, team, _carrier, _ball);
    }

    private void Step()
    {
        var pressured = false;
        if (Chance(0.16 + 0.22 * _ball.X / Length))
        {
            var (defender, _) = Nearest(Opp, _ball);
            Advance(Between(0.4, 1.2));
            Emit(MatchEventType.Pressure, Opp, defender, _ball);
            pressured = true;
            if (Chance(0.2))
            {
                Tackle(defender);
                return;
            }
        }

        var toGoal = Distance(_ball, Goal);
        if (_carrier.Role != Role.Goalkeeper && toGoal < 30)
        {
            var shotChance = toGoal < 12 ? 0.4 : toGoal < 18 ? 0.27 : toGoal < 25 ? 0.11 : 0.04;
            if (Chance(shotChance))
            {
                Shoot(pressured);
                return;
            }
        }

        if (_carrier.Role != Role.Goalkeeper && Chance(0.18))
            Carry();
        else
            Pass(pressured);
    }

    private void Pass(bool pressured)
    {
        var options = _clubs[_poss].Players
            .Where(p => p != _carrier && (p.Role != Role.Goalkeeper || _ball.X < 35))
            .Select(p => (Player: p, At: PositionOf(p, _poss)))
            .ToList();
        var weights = options.Select(o =>
        {
            var d = Distance(_ball, o.At);
            var forward = Math.Max(0, o.At.X - _ball.X);
            return Math.Exp(-d / 22) * (1 + forward / 15) * (d < 5 ? 0.2 : 1);
        }).ToList();
        var (target, at) = options[Pick(weights)];

        var distance = Distance(_ball, at);
        var success = 0.95 - 0.007 * Math.Max(0, distance - 8)
                           - (pressured ? 0.10 : 0)
                           - (at.X > 80 ? 0.10 : 0)
                           + (_carrier.Skill - 0.6) * 0.2;
        var ballSpeed = distance > 30 ? Between(19, 25) : Between(11, 17);
        var flight = Seconds(distance / ballSpeed);
        Advance(Between(0.8, 3.0));

        if (Chance(Math.Clamp(success, 0.3, 0.97)))
        {
            Emit(MatchEventType.Pass, _poss, _carrier, _ball, at, target.Id, EventOutcome.Success, pressured, flight);
            Advance(flight);
            _ball = at;
            _carrier = target;
            return;
        }

        var end = Clamp(new PitchPoint(at.X + Between(-5, 5), at.Y + Between(-5, 5)));
        if (Chance(0.7))
        {
            flight = Seconds(Distance(_ball, end) / ballSpeed);
            Emit(MatchEventType.Pass, _poss, _carrier, _ball, end, target.Id, EventOutcome.Failure, pressured, flight);
            var (interceptor, _) = Nearest(Opp, end);
            Advance(flight * 0.8);
            Emit(MatchEventType.Interception, Opp, interceptor, end, outcome: EventOutcome.Success);
            ChangePossession(interceptor, end);
        }
        else
        {
            var touchline = end with { Y = end.Y < Width / 2 ? 0 : Width };
            flight = Seconds(Distance(_ball, touchline) / ballSpeed);
            Emit(MatchEventType.Pass, _poss, _carrier, _ball, touchline, target.Id, EventOutcome.Failure, pressured, flight);
            var (thrower, _) = Nearest(Opp, touchline);
            Advance(flight + Seconds(Between(10, 25)));
            ChangePossession(thrower, touchline);
        }
    }

    private void Carry()
    {
        // Near the box players cut inside towards goal rather than running at the byline.
        var heading = _ball.X > 88 ? Math.Atan2(Goal.Y - _ball.Y, Goal.X - _ball.X) : 0;
        var angle = heading + Normal(0, 0.5);
        var length = Between(4, 14);
        var end = Clamp(new PitchPoint(
            Math.Min(_ball.X + length * Math.Cos(angle), 100),
            _ball.Y + length * Math.Sin(angle)));
        var duration = Seconds(Distance(_ball, end) / Between(4.5, 7));
        Emit(MatchEventType.Carry, _poss, _carrier, _ball, end, outcome: EventOutcome.Success, duration: duration);
        Advance(duration);
        _ball = end;
    }

    private void Tackle(Player defender)
    {
        Advance(Between(0.3, 1.0));
        if (Chance(0.55))
        {
            Emit(MatchEventType.Tackle, Opp, defender, _ball, target: _carrier.Id, outcome: EventOutcome.Success);
            ChangePossession(defender, _ball);
        }
        else if (Chance(0.35))
        {
            Emit(MatchEventType.Tackle, Opp, defender, _ball, target: _carrier.Id, outcome: EventOutcome.Failure);
            Emit(MatchEventType.Foul, Opp, defender, _ball, target: _carrier.Id);
            Advance(Between(15, 35));
        }
        else
        {
            Emit(MatchEventType.Tackle, Opp, defender, _ball, target: _carrier.Id, outcome: EventOutcome.Failure);
            Carry();
        }
    }

    private void Shoot(bool pressured)
    {
        var distance = Distance(_ball, Goal);
        // Distance-based xG, discounted for blocks and defenders on the line.
        var xg = 0.45 / (1 + Math.Exp(0.14 * distance - 0.9 + (pressured ? 0.4 : 0) - (_carrier.Skill - 0.7) * 2));
        var keeper = _clubs[Opp].Goalkeeper;
        var (outcome, end) = Chance(xg) ? (MatchEventType.Goal, new PitchPoint(Length, Width / 2 + Between(-3.4, 3.4)))
            : Chance(0.4) ? (MatchEventType.Save, new PitchPoint(Length - 1, Width / 2 + Between(-3.4, 3.4)))
            : (MatchEventType.Shot, new PitchPoint(Length, Width / 2 + Between(4.5, 12) * (Chance(0.5) ? 1 : -1)));
        var flight = Seconds(Distance(_ball, end) / Between(18, 32));
        Advance(Between(0.3, 1.0));

        var scored = outcome == MatchEventType.Goal;
        Emit(MatchEventType.Shot, _poss, _carrier, _ball, end,
            outcome: scored ? EventOutcome.Success : EventOutcome.Failure, pressured: pressured, duration: flight);
        Advance(flight);

        switch (outcome)
        {
            case MatchEventType.Goal:
                Emit(MatchEventType.Goal, _poss, _carrier, end, outcome: EventOutcome.Success);
                Advance(Between(45, 75));
                KickOff(Opp);
                break;
            case MatchEventType.Save:
                Emit(MatchEventType.Save, Opp, keeper, end, outcome: EventOutcome.Success);
                ChangePossession(keeper, end);
                Advance(Between(4, 10));
                break;
            default:
                Advance(Between(20, 40));
                ChangePossession(keeper, new PitchPoint(Length - 6, Width / 2));
                break;
        }
    }

    /// <summary>Hands the ball to <paramref name="player"/> of the other team. <paramref name="at"/> is in the current possessor's frame.</summary>
    private void ChangePossession(Player player, PitchPoint at)
    {
        _poss = Opp;
        _ball = Flip(at);
        _carrier = player;
        Emit(MatchEventType.PossessionChange, _poss, player, _ball);
    }

    /// <summary>Approximate position of a player in the possessing team's frame, shifted with the ball.</summary>
    private PitchPoint PositionOf(Player player, int team)
    {
        var inPossession = team == _poss;
        var ball = inPossession ? _ball : Flip(_ball);
        var shiftX = (ball.X - Length / 2) * (inPossession ? 0.45 : 0.35) - (inPossession ? 0 : 10);
        if (player.Role == Role.Goalkeeper)
            shiftX *= 0.1;
        var shiftY = (ball.Y - Width / 2) * 0.25;
        // Attackers cannot get much beyond the opposition's defensive line.
        var own = Clamp(new PitchPoint(
            Math.Min(player.Base.X + shiftX + Between(-3, 3), 92),
            player.Base.Y + shiftY + Between(-3, 3)));
        return inPossession ? own : Flip(own);
    }

    private (Player Player, PitchPoint At) Nearest(int team, PitchPoint point) =>
        _clubs[team].Players
            .Select(p => (Player: p, At: PositionOf(p, team)))
            .MinBy(p => Distance(p.At, point));

    private void Emit(
        MatchEventType type, int team, Player? player, PitchPoint start, PitchPoint? end = null,
        string? target = null, EventOutcome outcome = EventOutcome.None, bool pressured = false, TimeSpan? duration = null)
    {
        var sequence = _events.Count + 1;
        _events.Add(new MatchEvent(
            $"{_matchId}-{sequence:D5}", _matchId, sequence, _clock, _period, type, _clubs[team].Id, player?.Id,
            ToPitch(start), end is { } e ? ToPitch(e) : null, target, outcome, pressured, duration));
    }

    private int Opp => 1 - _poss;

    /// <summary>Home attacks towards x = 105 in the first half, away in the second.</summary>
    private PitchPoint ToPitch(PitchPoint frame) => (_poss == 0) == (_period == 1) ? Round(frame) : Round(Flip(frame));

    private static PitchPoint Flip(PitchPoint p) => new(Length - p.X, Width - p.Y);
    private static PitchPoint Clamp(PitchPoint p) => new(Math.Clamp(p.X, 0, Length), Math.Clamp(p.Y, 0, Width));
    private static PitchPoint Round(PitchPoint p) => new(Math.Round(p.X, 1), Math.Round(p.Y, 1));
    private static double Distance(PitchPoint a, PitchPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static TimeSpan Seconds(double s) => TimeSpan.FromMilliseconds(Math.Round(s * 1000));

    private void Advance(double seconds) => _clock += Seconds(seconds);
    private void Advance(TimeSpan duration) => _clock += duration;
    private bool Chance(double p) => _rng.NextDouble() < p;
    private double Between(double min, double max) => min + _rng.NextDouble() * (max - min);

    private double Normal(double mean, double sd)
    {
        var u1 = 1 - _rng.NextDouble();
        var u2 = _rng.NextDouble();
        return mean + sd * Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    private int Pick(IReadOnlyList<double> weights)
    {
        var roll = _rng.NextDouble() * weights.Sum();
        for (var i = 0; i < weights.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0)
                return i;
        }
        return weights.Count - 1;
    }
}
