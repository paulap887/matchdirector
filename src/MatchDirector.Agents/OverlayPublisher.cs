using System.Net.Http.Json;
using MatchDirector.Contracts;
using MatchDirector.StatsEngine;

namespace MatchDirector.Agents;

/// <summary>Sends overlays, live stats and match info to the overlay hub, which pushes them to viewers over SignalR.</summary>
public sealed class OverlayPublisher(HttpClient http, ILogger<OverlayPublisher> logger)
{
    public const string IngestKeyHeader = "X-Ingest-Key";

    public Task PublishAsync(MatchInfo info, CancellationToken ct) => PostAsync("api/match-info", info, ct);
    public Task PublishAsync(Overlay overlay, CancellationToken ct) => PostAsync("api/overlays", overlay, ct);
    public Task PublishAsync(MatchSnapshot snapshot, CancellationToken ct) => PostAsync("api/snapshots", snapshot, ct);

    private async Task PostAsync<T>(string path, T body, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(path, body, MatchDirectorJson.Options, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Overlay hub rejected {Path}: {Status}", path, response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Overlay hub unreachable for {Path}", path);
        }
    }
}
