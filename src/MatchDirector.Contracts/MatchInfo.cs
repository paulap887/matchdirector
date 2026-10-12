namespace MatchDirector.Contracts;

/// <summary>Published once before kick-off on the same stream as the events, so consumers know the teams and players.</summary>
public sealed record MatchInfo(string MatchId, string Venue, TeamInfo Home, TeamInfo Away);

public sealed record TeamInfo(string TeamId, string Name, IReadOnlyList<PlayerInfo> Players);

public sealed record PlayerInfo(string PlayerId, string Name, int Number, string Position);

/// <summary>Message kinds on the match stream, carried in the <c>kind</c> application property.</summary>
public static class MessageKinds
{
    public const string Property = "kind";
    public const string MatchInfo = "match-info";
    public const string MatchEvent = "match-event";
}
