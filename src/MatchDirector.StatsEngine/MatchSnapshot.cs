namespace MatchDirector.StatsEngine;

public sealed record TeamStats(
    string TeamId, int Goals, int Shots, int ShotsOnTarget, double Xg, int Passes, int PassesCompleted,
    double PassAccuracy, double Possession, int ProgressivePasses, int Tackles, int Interceptions, int Pressures, int Fouls);

public sealed record PlayerStats(
    string PlayerId, string TeamId, int Passes, int PassesCompleted, int ProgressivePasses, int Shots, int Goals,
    double Xg, double DistanceCarried, int Tackles, int Interceptions, double? FastestShot, double? TopCarrySpeed = null);

public sealed record MatchSnapshot(
    string MatchId, TimeSpan MatchClock, int Period, TeamStats Home, TeamStats Away,
    double Momentum, double Chaos, IReadOnlyList<PlayerStats> Players);
