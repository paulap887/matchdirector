using System.Diagnostics;
using MatchDirector.Simulator.Engine;
using Microsoft.Extensions.Options;

namespace MatchDirector.Simulator;

/// <summary>
/// Plays simulated matches against the match clock, scaled by <see cref="SimulatorOptions.Speed"/>. With an overlay hub,
/// matches play on demand: one match per kick-off request, paused if nobody has been watching for a while.
/// </summary>
public sealed class Worker(IOptions<SimulatorOptions> options, IMatchEventSink sink, IMatchDemand demand, ILogger<Worker> logger)
    : BackgroundService
{
    private static readonly TimeSpan DemandCheck = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var seed = settings.Seed ?? Random.Shared.Next(1, 1_000_000);
        var speed = Math.Clamp(settings.Speed, 1, 20);

        do
        {
            await demand.ReportAsync("idle", stoppingToken);
            await demand.WaitForKickoffAsync(stoppingToken);

            var match = new MatchEngine(seed, Clubs.KestrelBay, Clubs.RedmoorRovers).Play();
            logger.LogInformation("Kick-off {MatchId}: {Home} v {Away} at {Venue}, {Count} events, {Speed}x",
                match.MatchId, match.Home.Name, match.Away.Name, match.Home.Venue, match.Events.Count, speed);

            await demand.ReportAsync("playing", stoppingToken);
            await sink.PublishAsync(match.Info, stoppingToken);
            var finished = await PlayAsync(match, speed, settings.IdleTimeout, stoppingToken);

            var goals = match.Events.Where(e => e.Type == Contracts.MatchEventType.Goal).ToList();
            logger.LogInformation(finished ? "Full time {MatchId}: {Home} {HomeGoals}-{AwayGoals} {Away}" : "Paused {MatchId}: nobody watching",
                match.MatchId, match.Home.Name, goals.Count(g => g.TeamId == match.Home.Id),
                goals.Count(g => g.TeamId == match.Away.Id), match.Away.Name);
            seed++;

            // Hold the next kick-off so the full-time report (written about 15 s after the whistle) stays on screen.
            if (finished && settings.Loop)
            {
                await demand.ReportAsync("idle", stoppingToken);
                await Task.Delay(settings.FullTimeBreak, stoppingToken);
            }
        }
        while (settings.Loop && !stoppingToken.IsCancellationRequested);
    }

    /// <summary>Plays a match in real time. Returns false if it was stopped early because nobody was watching.</summary>
    private async Task<bool> PlayAsync(SimulatedMatch match, double speed, TimeSpan idleTimeout, CancellationToken cancellationToken)
    {
        // The clock restarts at 45:00 for the second half, so only forward movement counts as elapsed time.
        var elapsed = TimeSpan.Zero;
        var previous = TimeSpan.Zero;
        var started = Stopwatch.GetTimestamp();
        var lastCheck = Stopwatch.GetTimestamp();
        long? unwatchedSince = null;

        foreach (var e in match.Events)
        {
            if (e.MatchClock > previous)
                elapsed += e.MatchClock - previous;
            previous = e.MatchClock;

            var wait = elapsed / speed - Stopwatch.GetElapsedTime(started);
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, cancellationToken);

            await sink.PublishAsync(e, cancellationToken);

            if (Stopwatch.GetElapsedTime(lastCheck) < DemandCheck)
                continue;
            lastCheck = Stopwatch.GetTimestamp();
            if (await demand.ViewersAsync(cancellationToken) > 0)
                unwatchedSince = null;
            else if ((unwatchedSince ??= Stopwatch.GetTimestamp()) is var since && Stopwatch.GetElapsedTime(since) > idleTimeout)
                return false;
        }
        return true;
    }
}
