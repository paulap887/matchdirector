using System.Text.Json;
using Microsoft.AspNetCore.SignalR;

namespace MatchDirector.OverlayHub;

/// <summary>Viewers connect here; the server pushes matchInfo, snapshot, overlay and status messages.</summary>
public sealed class MatchHub(Demand demand) : Hub
{
    public override Task OnConnectedAsync()
    {
        demand.ViewerJoined();
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        demand.ViewerLeft();
        return base.OnDisconnectedAsync(exception);
    }
}

/// <summary>Latest broadcast state, replayed to viewers who connect mid-match.</summary>
public sealed class BroadcastState
{
    private const int RecentOverlays = 20;
    private readonly Lock _lock = new();

    private readonly List<JsonElement> _overlays = [];
    private JsonElement? _matchInfo;

    public JsonElement? Snapshot { get; set; }

    /// <summary>Records the match on air. Only a different match clears the screen; repeats of the same one just refresh it.</summary>
    public void Start(JsonElement matchInfo)
    {
        lock (_lock)
        {
            if (MatchId(_matchInfo) != MatchId(matchInfo))
            {
                Snapshot = null;
                _overlays.Clear();
            }
            _matchInfo = matchInfo;
        }
    }

    private static string? Id(JsonElement overlay) =>
        overlay.TryGetProperty("overlayId", out var id) ? id.GetString() : null;

    private static string? MatchId(JsonElement? info) =>
        info is { ValueKind: JsonValueKind.Object } i && i.TryGetProperty("matchId", out var id) ? id.GetString() : null;

    /// <summary>Adds an overlay, or replaces it in place when the review agents republish it with the same ID.</summary>
    public void Upsert(JsonElement overlay)
    {
        lock (_lock)
        {
            var id = Id(overlay);
            var existing = _overlays.FindIndex(o => Id(o) == id);
            if (existing >= 0)
            {
                _overlays[existing] = overlay;
                return;
            }
            _overlays.Add(overlay);
            if (_overlays.Count > RecentOverlays)
                _overlays.RemoveAt(0);
        }
    }

    public object Current(string status)
    {
        lock (_lock)
            return new { matchInfo = _matchInfo, snapshot = Snapshot, overlays = _overlays.ToList(), status };
    }
}
