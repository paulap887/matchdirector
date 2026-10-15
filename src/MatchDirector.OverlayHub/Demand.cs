namespace MatchDirector.OverlayHub;

/// <summary>
/// Whether anyone wants a match. Matches play on demand: a viewer connecting (or pressing "kick off") requests one,
/// the simulator claims the request and plays one match, and an open tab on its own never keeps matches running.
/// </summary>
public sealed class Demand
{
    private readonly Lock _lock = new();
    private int _viewers;
    private bool _kickoffRequested;

    public int Viewers => Volatile.Read(ref _viewers);

    /// <summary>"idle" or "playing", as last reported by the simulator.</summary>
    public string Status { get; private set; } = "idle";

    public void ViewerJoined()
    {
        Interlocked.Increment(ref _viewers);
        RequestKickoff();
    }

    public void ViewerLeft() => Interlocked.Decrement(ref _viewers);

    public void RequestKickoff()
    {
        lock (_lock)
            _kickoffRequested = true;
    }

    /// <summary>The simulator takes the pending request, if there is one and someone is watching.</summary>
    public bool TryClaimKickoff()
    {
        lock (_lock)
        {
            if (!_kickoffRequested || Viewers <= 0)
                return false;
            _kickoffRequested = false;
            return true;
        }
    }

    public void Report(string status) => Status = status == "playing" ? "playing" : "idle";
}
