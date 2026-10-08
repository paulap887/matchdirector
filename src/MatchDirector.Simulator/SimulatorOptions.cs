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
}
