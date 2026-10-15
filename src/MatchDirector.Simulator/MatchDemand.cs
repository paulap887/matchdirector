using System.Net.Http.Json;

namespace MatchDirector.Simulator;

/// <summary>Whether anyone wants a match played, so the studio (and its models) only run when someone is watching.</summary>
public interface IMatchDemand
{
    /// <summary>Waits until a viewer has asked for a match.</summary>
    Task WaitForKickoffAsync(CancellationToken cancellationToken);

    Task<int> ViewersAsync(CancellationToken cancellationToken);

    Task ReportAsync(string status, CancellationToken cancellationToken);
}

/// <summary>Plays matches back to back, for running the simulator on its own.</summary>
public sealed class ContinuousDemand : IMatchDemand
{
    public Task WaitForKickoffAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<int> ViewersAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    public Task ReportAsync(string status, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Asks the overlay hub, which counts connected viewers and holds at most one pending kick-off request.</summary>
public sealed class HubDemand(HttpClient http, ILogger<HubDemand> logger) : IMatchDemand
{
    public const string IngestKeyHeader = "X-Ingest-Key";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    public async Task WaitForKickoffAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Waiting for a viewer to kick off a match");
        while (true)
        {
            try
            {
                using var response = await http.PostAsync("api/demand/claim", null, cancellationToken);
                if (response.IsSuccessStatusCode && (await response.Content.ReadFromJsonAsync<Claim>(cancellationToken))?.Claimed == true)
                    return;
            }
            catch (HttpRequestException ex)
            {
                logger.LogDebug(ex, "Overlay hub unreachable; staying idle");
            }
            await Task.Delay(PollInterval, cancellationToken);
        }
    }

    public async Task<int> ViewersAsync(CancellationToken cancellationToken)
    {
        try
        {
            return (await http.GetFromJsonAsync<Viewers>("api/demand", cancellationToken))?.Count ?? 0;
        }
        catch (HttpRequestException)
        {
            return 0; // no hub, no audience
        }
    }

    public async Task ReportAsync(string status, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("api/status", new { status }, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogDebug(ex, "Could not report status {Status}", status);
        }
    }

    private sealed record Claim(bool Claimed, int Viewers);

    private sealed record Viewers([property: System.Text.Json.Serialization.JsonPropertyName("viewers")] int Count);
}
