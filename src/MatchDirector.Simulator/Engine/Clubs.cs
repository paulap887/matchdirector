using MatchDirector.Contracts;

namespace MatchDirector.Simulator.Engine;

public enum Role { Goalkeeper, Defender, Midfielder, Forward }

/// <summary>A fictional player. <see cref="Base"/> is the in-possession 4-3-3 slot in the team's attacking frame (own goal at x = 0).</summary>
public sealed record Player(string Id, string Name, int Number, Role Role, PitchPoint Base, double Skill);

public sealed record Club(string Id, string Name, string Venue, IReadOnlyList<Player> Players)
{
    public Player Goalkeeper => Players.First(p => p.Role == Role.Goalkeeper);
    public Player Striker => Players.Single(p => p.Number == 9);
}

/// <summary>Fictional clubs. Any resemblance to real clubs or players is unintended.</summary>
public static class Clubs
{
    private static readonly PitchPoint[] FourThreeThree =
    [
        new(5, 34),                                                // GK
        new(32, 58), new(24, 42), new(24, 26), new(32, 10),       // RB, CB, CB, LB
        new(46, 34), new(56, 48), new(56, 20),                    // CM x3
        new(78, 58), new(84, 34), new(78, 10)                     // RW, ST, LW
    ];

    private static readonly (int Number, Role Role)[] Slots =
    [
        (1, Role.Goalkeeper),
        (2, Role.Defender), (4, Role.Defender), (5, Role.Defender), (3, Role.Defender),
        (6, Role.Midfielder), (8, Role.Midfielder), (10, Role.Midfielder),
        (7, Role.Forward), (9, Role.Forward), (11, Role.Forward)
    ];

    public static Club KestrelBay { get; } = Build("KBY", "Kestrel Bay FC", "Lantern Park",
    [
        ("Emil Strand", 0.70), ("Jonah Achterberg", 0.62), ("Kofi Mensal", 0.68), ("Ruairi Callow", 0.64),
        ("Mateo Ferrin", 0.66), ("Sami Haddadi", 0.74), ("Luca Benedetto-Ray", 0.70), ("Yusuf Kareth", 0.82),
        ("Theo Marchetti-Vance", 0.76), ("Dario Okonkwe", 0.80), ("Elias Brandvold", 0.72)
    ]);

    public static Club RedmoorRovers { get; } = Build("RDM", "Redmoor Rovers", "The Ironworks",
    [
        ("Piotr Halvik", 0.72), ("Callum Wrexley", 0.60), ("Adebayo Stroud", 0.70), ("Henrik Lysgaard", 0.66),
        ("Nico Valcourt", 0.63), ("Omar Zeidan-Holt", 0.71), ("Finn Arkwright", 0.67), ("Rafael Ondine", 0.78),
        ("Kai Nakamura-Reid", 0.74), ("Bastian Corvel", 0.77), ("Ilyas Benhamed", 0.75)
    ]);

    private static Club Build(string id, string name, string venue, (string Name, double Skill)[] squad) =>
        new(id, name, venue, squad.Select((p, i) => new Player(
            $"{id}-{Slots[i].Number:D2}", p.Name, Slots[i].Number, Slots[i].Role, FourThreeThree[i], p.Skill)).ToList());
}
