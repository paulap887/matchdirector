using MatchDirector.Simulator.Engine;

namespace MatchDirector.StatsEngine.Tests;

public class PlayerRatingsTests
{
    [Fact]
    public void Rating_follows_the_documented_formula()
    {
        var p = new PlayerStats("KBY-09", "KBY", Passes: 30, PassesCompleted: 25, ProgressivePasses: 4, Shots: 5, Goals: 2,
            Xg: 1.2, DistanceCarried: 120, Tackles: 1, Interceptions: 2, FastestShot: 28);

        // 3*2 + 2*1.2 + 0.15*4 + 0.3*3 + 0.02*25 + 0.005*120 = 6 + 2.4 + 0.6 + 0.9 + 0.5 + 0.6
        Assert.Equal(11.0, PlayerRatings.Rate(p));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(17)]
    public void Ranking_is_deterministic_and_complete(int seed)
    {
        MatchSnapshot Final()
        {
            var match = new MatchEngine(seed, Clubs.KestrelBay, Clubs.RedmoorRovers).Play();
            var analyzer = new MatchAnalyzer(match.MatchId, match.Home.Id, match.Away.Id);
            foreach (var e in match.Events)
                analyzer.Ingest(e);
            return analyzer.Snapshot();
        }

        var first = PlayerRatings.Rank(Final());
        Assert.Equal(first, PlayerRatings.Rank(Final()));
        Assert.Equal(first.Select(r => r.Rating).OrderDescending(), first.Select(r => r.Rating));
        Assert.Equal(22, first.Count);
    }
}
