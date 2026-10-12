using System.Text.Json;
using Microsoft.AspNetCore.SignalR;

namespace MatchDirector.OverlayHub;

/// <summary>Viewers connect here; the server pushes matchInfo, snapshot and overlay messages.</summary>
public sealed class MatchHub : Hub;

/// <summary>Latest broadcast state, replayed to viewers who connect mid-match.</summary>
public sealed class BroadcastState
{
    private const int RecentOverlays = 20;
    private readonly Lock _lock = new();

    private readonly List<JsonElement> _overlays = [];
    private JsonElement? _matchInfo;

    public JsonElement? Snapshot { get; set; }

    public void Start(JsonElement matchInfo)
    {
        lock (_lock)
        {
            _matchInfo = matchInfo;
            Snapshot = null;
            _overlays.Clear();
        }
    }

    public void Add(JsonElement overlay)
    {
        lock (_lock)
        {
            _overlays.Add(overlay);
            if (_overlays.Count > RecentOverlays)
                _overlays.RemoveAt(0);
        }
    }

    public object Current()
    {
        lock (_lock)
            return new { matchInfo = _matchInfo, snapshot = Snapshot, overlays = _overlays.ToList() };
    }
}
