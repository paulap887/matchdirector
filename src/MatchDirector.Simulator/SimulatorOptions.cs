namespace MatchDirector.Simulator;

public sealed class SimulatorOptions
{
    public const string Section = "Simulator";

    /// <summary>Fixed seed for a reproducible match. A random seed is used when empty.</summary>
    public int? Seed { get; set; }

    /// <summary>Replay speed: 1 is real time, 20 plays a half in a little over two minutes.</summary>
    public double Speed { get; set; } = 10;

    /// <summary>Start a new match (next seed) when the current one ends.</summary>
    public bool Loop { get; set; } = true;

    /// <summary>Play matches only when a viewer asks for one (needs the overlay hub). Saves model cost when nobody is watching.</summary>
    public bool OnDemand { get; set; } = true;

    /// <summary>Pause after full time before another match can start, so viewers see the full-time report.</summary>
    public TimeSpan FullTimeBreak { get; set; } = TimeSpan.FromSeconds(45);

    /// <summary>With <see cref="OnDemand"/>, pause a match once nobody has been watching for this long.</summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
