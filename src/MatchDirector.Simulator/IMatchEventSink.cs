using MatchDirector.Contracts;

namespace MatchDirector.Simulator;

/// <summary>Where simulated events go: logs locally, Event Hubs when deployed.</summary>
public interface IMatchEventSink
{
    ValueTask PublishAsync(MatchInfo matchInfo, CancellationToken cancellationToken);

    ValueTask PublishAsync(MatchEvent matchEvent, CancellationToken cancellationToken);
}

public sealed class LoggingEventSink(ILogger<LoggingEventSink> logger) : IMatchEventSink
{
    public ValueTask PublishAsync(MatchInfo info, CancellationToken cancellationToken)
    {
        logger.LogInformation("Match {MatchId}: {Home} v {Away} at {Venue}", info.MatchId, info.Home.Name, info.Away.Name, info.Venue);
        return ValueTask.CompletedTask;
    }

    public ValueTask PublishAsync(MatchEvent e, CancellationToken cancellationToken)
    {
        var level = e.Type is MatchEventType.Goal or MatchEventType.Shot or MatchEventType.KickOff or MatchEventType.PeriodEnd
            ? LogLevel.Information
            : LogLevel.Debug;
        logger.Log(level, "{Clock:mm\\:ss} {Type} {Team} {Player} {Outcome} ({EventId})",
            e.MatchClock, e.Type, e.TeamId, e.PlayerId, e.Outcome, e.EventId);
        return ValueTask.CompletedTask;
    }
}
