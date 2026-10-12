using System.Text.Json;
using Azure.Messaging.EventHubs.Consumer;
using MatchDirector.Contracts;

namespace MatchDirector.Agents;

/// <summary>Reads the live match stream from Event Hubs and hands it to the director.</summary>
public sealed class Worker(EventHubConsumerClient consumer, Director director, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Listening on {Hub} as consumer group {Group}", consumer.EventHubName, consumer.ConsumerGroup);
        var narration = director.RunNarrationAsync(stoppingToken);

        var options = new ReadEventOptions { MaximumWaitTime = TimeSpan.FromSeconds(5) };
        await foreach (var partitionEvent in consumer.ReadEventsAsync(startReadingAtEarliestEvent: false, options, stoppingToken))
        {
            if (partitionEvent.Data is not { } data)
                continue;

            var body = data.EventBody.ToMemory().Span;
            data.Properties.TryGetValue(MessageKinds.Property, out var kind);
            if (kind as string == MessageKinds.MatchInfo)
            {
                if (JsonSerializer.Deserialize<MatchInfo>(body, MatchDirectorJson.Options) is { } info)
                    await director.StartMatchAsync(info, stoppingToken);
            }
            else if (JsonSerializer.Deserialize<MatchEvent>(body, MatchDirectorJson.Options) is { } e)
            {
                await director.IngestAsync(e, stoppingToken);
            }
        }

        await narration;
    }
}
