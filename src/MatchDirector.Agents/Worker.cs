using System.Text.Json;
using Azure.Messaging.EventHubs.Consumer;
using MatchDirector.Contracts;

namespace MatchDirector.Agents;

/// <summary>
/// Reads the live match stream from Event Hubs and hands it to the director. On start it re-reads a short window of
/// history so a restart mid-match rebuilds the stats silently and then carries on live, instead of going quiet.
/// </summary>
public sealed class Worker(EventHubConsumerClient consumer, Director director, IConfiguration configuration, ILogger<Worker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var catchUp = TimeSpan.FromMinutes(configuration.GetValue("Agents:CatchUpMinutes", 20));
        var partitions = await consumer.GetPartitionIdsAsync(stoppingToken);
        logger.LogInformation("Listening on {Hub} ({Partitions} partitions) as {Group}, catching up {Minutes} min",
            consumer.EventHubName, partitions.Length, consumer.ConsumerGroup, catchUp.TotalMinutes);

        var readers = partitions.Select(p => ReadPartitionAsync(p, startedAt - catchUp, startedAt, stoppingToken));
        await Task.WhenAll(readers.Append(director.RunNarrationAsync(stoppingToken)).Append(director.RunReviewsAsync(stoppingToken)).Append(director.RunRecapsAsync(stoppingToken))
            .Append(director.RunTranslationsAsync(stoppingToken)));
    }

    private async Task ReadPartitionAsync(string partition, DateTimeOffset from, DateTimeOffset liveFrom, CancellationToken ct)
    {
        var options = new ReadEventOptions { MaximumWaitTime = TimeSpan.FromSeconds(5) };
        await foreach (var partitionEvent in consumer.ReadEventsFromPartitionAsync(partition, EventPosition.FromEnqueuedTime(from), options, ct))
        {
            if (partitionEvent.Data is not { } data)
                continue;

            var live = data.EnqueuedTime >= liveFrom;
            var body = data.EventBody.ToMemory().Span;
            data.Properties.TryGetValue(MessageKinds.Property, out var kind);
            if (kind as string == MessageKinds.MatchInfo)
            {
                if (JsonSerializer.Deserialize<MatchInfo>(body, MatchDirectorJson.Options) is { } info)
                    await director.StartMatchAsync(info, live, ct);
            }
            else if (JsonSerializer.Deserialize<MatchEvent>(body, MatchDirectorJson.Options) is { } e)
            {
                await director.IngestAsync(e, live, ct);
            }
        }
    }
}
