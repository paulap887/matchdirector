using System.Text.Json;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using MatchDirector.Contracts;

namespace MatchDirector.Simulator;

/// <summary>Publishes events to Event Hubs, partitioned by match so each match stays in order.</summary>
public sealed class EventHubsEventSink(EventHubProducerClient producer) : IMatchEventSink
{
    public async ValueTask PublishAsync(MatchEvent matchEvent, CancellationToken cancellationToken)
    {
        var data = new EventData(JsonSerializer.SerializeToUtf8Bytes(matchEvent, MatchDirectorJson.Options))
        {
            ContentType = "application/json",
            MessageId = matchEvent.EventId
        };
        await producer.SendAsync([data], new SendEventOptions { PartitionKey = matchEvent.MatchId }, cancellationToken);
    }
}
