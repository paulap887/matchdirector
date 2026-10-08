using System.Diagnostics;
using MatchDirector.Simulator.Engine;
using Microsoft.Extensions.Options;

namespace MatchDirector.Simulator;

/// <summary>Plays simulated matches against the match clock, scaled by <see cref="SimulatorOptions.Speed"/>.</summary>
public sealed class Worker(IOptions<SimulatorOptions> options, IMatchEventSink sink, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var seed = settings.Seed ?? Random.Shared.Next(1, 1_000_000);
        var speed = Math.Clamp(settings.Speed, 1, 20);

        do
        {
            var match = new MatchEngine(seed, Clubs.KestrelBay, Clubs.RedmoorRovers).Play();
            logger.LogInformation("Kick-off {MatchId}: {Home} v {Away} at {Venue}, {Count} events, {Speed}x",
                match.MatchId, match.Home.Name, match.Away.Name, match.Home.Venue, match.Events.Count, speed);

            await PlayAsync(match, speed, stoppingToken);

            var goals = match.Events.Where(e => e.Type == Contracts.MatchEventType.Goal).ToList();
            logger.LogInformation("Full time {MatchId}: {Home} {HomeGoals}-{AwayGoals} {Away}",
                match.MatchId, match.Home.Name, goals.Count(g => g.TeamId == match.Home.Id),
                goals.Count(g => g.TeamId == match.Away.Id), match.Away.Name);
            seed++;
        }
        while (settings.Loop && !stoppingToken.IsCancellationRequested);
    }

    private async Task PlayAsync(SimulatedMatch match, double speed, CancellationToken cancellationToken)
    {
        // The clock restarts at 45:00 for the second half, so only forward movement counts as elapsed time.
        var elapsed = TimeSpan.Zero;
        var previous = TimeSpan.Zero;
        var started = Stopwatch.GetTimestamp();

        foreach (var e in match.Events)
        {
            if (e.MatchClock > previous)
                elapsed += e.MatchClock - previous;
            previous = e.MatchClock;

            var wait = elapsed / speed - Stopwatch.GetElapsedTime(started);
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, cancellationToken);

            await sink.PublishAsync(e, cancellationToken);
        }
    }
}
