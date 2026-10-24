using System.Net.Http.Json;
using System.Text.Json;
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
    public Task PublishAsync(MatchRecap recap, CancellationToken ct) => PostAsync("api/recaps", recap, ct);

    /// <summary>
    /// The languages viewers are watching in right now, besides English. Empty if the hub cannot be reached or gives an
    /// unexpected answer, for example an older hub still running during a deployment, which serves its web page instead.
    /// </summary>
    public async Task<IReadOnlyList<string>> LanguagesAsync(CancellationToken ct)
    {
        try
        {
            return await http.GetFromJsonAsync<string[]>("api/languages", MatchDirectorJson.Options, ct) ?? [];
        }
        catch (HttpRequestException ex)
        {
            logger.LogDebug(ex, "Overlay hub unreachable for languages");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Overlay hub gave no usable language list: {Error}", ex.Message);
        }
        return [];
    }

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
