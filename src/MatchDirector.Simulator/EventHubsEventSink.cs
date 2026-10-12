using System.Text.Json;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using MatchDirector.Contracts;

namespace MatchDirector.Simulator;

/// <summary>Publishes events to Event Hubs, partitioned by match so each match stays in order.</summary>
public sealed class EventHubsEventSink(EventHubProducerClient producer) : IMatchEventSink
{
    public ValueTask PublishAsync(MatchInfo matchInfo, CancellationToken cancellationToken) =>
        SendAsync(matchInfo, MessageKinds.MatchInfo, $"{matchInfo.MatchId}-info", matchInfo.MatchId, cancellationToken);

    public ValueTask PublishAsync(MatchEvent matchEvent, CancellationToken cancellationToken) =>
        SendAsync(matchEvent, MessageKinds.MatchEvent, matchEvent.EventId, matchEvent.MatchId, cancellationToken);

    private async ValueTask SendAsync<T>(T body, string kind, string messageId, string matchId, CancellationToken cancellationToken)
    {
        var data = new EventData(JsonSerializer.SerializeToUtf8Bytes(body, MatchDirectorJson.Options))
        {
            ContentType = "application/json",
            MessageId = messageId,
            Properties = { [MessageKinds.Property] = kind }
        };
        await producer.SendAsync([data], new SendEventOptions { PartitionKey = matchId }, cancellationToken);
    }
}
