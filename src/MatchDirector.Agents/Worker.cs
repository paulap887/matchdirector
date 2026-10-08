using System.Text.Json;
using Azure.Messaging.EventHubs.Consumer;
using MatchDirector.Contracts;

namespace MatchDirector.Agents;

/// <summary>Reads live match events from Event Hubs. Moments from the stats engine will be routed to the agent team from here.</summary>
public sealed class Worker(EventHubConsumerClient consumer, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Listening on {Hub} as consumer group {Group}", consumer.EventHubName, consumer.ConsumerGroup);

        var options = new ReadEventOptions { MaximumWaitTime = TimeSpan.FromSeconds(5) };
        await foreach (var partitionEvent in consumer.ReadEventsAsync(startReadingAtEarliestEvent: false, options, stoppingToken))
        {
            if (partitionEvent.Data is null)
                continue;

            var e = JsonSerializer.Deserialize<MatchEvent>(partitionEvent.Data.EventBody.ToMemory().Span, MatchDirectorJson.Options);
            if (e is null)
                continue;

            var level = e.Type is MatchEventType.Goal or MatchEventType.KickOff or MatchEventType.PeriodEnd
                ? LogLevel.Information
                : LogLevel.Debug;
            logger.Log(level, "{Clock:mm\\:ss} {Type} {Team} {Player} ({EventId})", e.MatchClock, e.Type, e.TeamId, e.PlayerId, e.EventId);
        }
    }
}
