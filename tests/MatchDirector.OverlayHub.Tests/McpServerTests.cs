using System.Net.Http.Json;
using System.Text.Json;
using MatchDirector.Contracts;
using MatchDirector.Simulator.Engine;
using MatchDirector.StatsEngine;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MatchDirector.OverlayHub.Tests;

/// <summary>
/// The hub's MCP server end to end: a simulated match is fed in through the hub's own ingest API, then a real MCP
/// client lists and calls the tools over HTTP.
/// </summary>
public sealed class McpServerTests : IClassFixture<McpServerTests.Hub>
{
    private const string IngestKey = "test-ingest-key";
    private readonly Hub _hub;

    public McpServerTests(Hub hub) => _hub = hub;

    [Fact]
    public async Task Lists_four_read_only_tools()
    {
        await using var client = await _hub.ClientAsync();
        var tools = await client.ListToolsAsync();

        Assert.Equal(["get_full_time_report", "get_match_state", "get_player_stats", "get_recent_moments"], tools.Select(t => t.Name).Order());
        Assert.All(tools, t => Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint));
    }

    [Fact]
    public async Task Match_state_carries_the_stats_engine_numbers()
    {
        var match = await _hub.PlayAsync();
        await using var client = await _hub.ClientAsync();

        var state = await CallAsync(client, "get_match_state");

        Assert.Equal(match.Info.MatchId, state.GetProperty("matchId").GetString());
        Assert.Equal("Kestrel Bay FC", state.GetProperty("home").GetProperty("name").GetString());
        Assert.Equal(match.Final.Home.Goals, state.GetProperty("home").GetProperty("goals").GetInt32());
        Assert.Equal(match.Final.Away.Xg, state.GetProperty("away").GetProperty("xg").GetDouble());
    }

    [Fact]
    public async Task Moments_come_newest_first_with_their_evidence()
    {
        var match = await _hub.PlayAsync();
        await using var client = await _hub.ClientAsync();

        var result = await CallAsync(client, "get_recent_moments", new() { ["count"] = 3 });
        var moments = result.GetProperty("moments").EnumerateArray().ToList();

        Assert.Equal(3, moments.Count);
        Assert.Equal(match.Published[^1].Type, moments[0].GetProperty("type").GetString());
        Assert.All(moments, m => Assert.NotEmpty(m.GetProperty("evidenceEventIds").EnumerateArray()));

        var goals = await CallAsync(client, "get_recent_moments", new() { ["type"] = "goal", ["count"] = 20 });
        Assert.All(goals.GetProperty("moments").EnumerateArray(), m => Assert.Equal("Goal", m.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task A_player_is_found_by_name_or_shirt_number()
    {
        var match = await _hub.PlayAsync();
        await using var client = await _hub.ClientAsync();
        var player = match.Info.Home.Players[0];
        var expected = match.Final.Players.First(p => p.PlayerId == player.PlayerId);

        var byName = await CallAsync(client, "get_player_stats", new() { ["player"] = player.Name.Split(' ')[^1] });
        Assert.Equal(player.Name, byName.GetProperty("name").GetString());
        Assert.Equal(expected.Passes, byName.GetProperty("stats").GetProperty("passes").GetInt32());

        var unknown = await CallAsync(client, "get_player_stats", new() { ["player"] = "Nobody At All" });
        Assert.True(unknown.TryGetProperty("error", out _));
    }

    private static async Task<JsonElement> CallAsync(McpClient client, string tool, Dictionary<string, object?>? arguments = null)
    {
        var result = await client.CallToolAsync(tool, arguments ?? []);
        Assert.NotEqual(true, result.IsError);
        return JsonDocument.Parse(Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text).RootElement;
    }

    /// <summary>The hub in memory, with a match fed in through its ingest API.</summary>
    public sealed class Hub : WebApplicationFactory<MatchTools> // any hub type locates the app
    {
        private Played? _played;

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
            builder.UseSetting("Overlay:IngestKey", IngestKey);

        public async Task<McpClient> ClientAsync() =>
            await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(Server.BaseAddress, "/mcp") }, CreateClient()));

        /// <summary>Plays one seeded match through the stats engine and posts what the director would: info, final stats, overlays.</summary>
        public async Task<Played> PlayAsync()
        {
            if (_played is not null)
                return _played;

            var match = new MatchEngine(42, Clubs.KestrelBay, Clubs.RedmoorRovers).Play();
            var analyzer = new MatchAnalyzer(match.MatchId, match.Info.Home.TeamId, match.Info.Away.TeamId);
            var moments = match.Events.SelectMany(analyzer.Ingest).ToList();
            var published = moments.TakeLast(10).Select(m => new Overlay(m.MomentId, match.MatchId, m.MatchClock, m.MatchClock, 6000,
                m.Type.ToString(), m.Priority, m.PlayerId, m.Metrics, m.Evidence,
                new Dictionary<string, string> { ["casual"] = $"{m.Type}", ["analyst"] = $"{m.Type} at {m.MatchClock}", ["source"] = "template" },
                m.Participants)).ToList();

            var http = CreateClient();
            http.DefaultRequestHeaders.Add("X-Ingest-Key", IngestKey);
            (await http.PostAsJsonAsync("/api/match-info", match.Info, MatchDirectorJson.Options)).EnsureSuccessStatusCode();
            (await http.PostAsJsonAsync("/api/snapshots", analyzer.Snapshot(), MatchDirectorJson.Options)).EnsureSuccessStatusCode();
            foreach (var overlay in published)
                (await http.PostAsJsonAsync("/api/overlays", overlay, MatchDirectorJson.Options)).EnsureSuccessStatusCode();

            return _played = new Played(match, analyzer.Snapshot(), published);
        }
    }

    public sealed record Played(SimulatedMatch Match, MatchSnapshot Final, IReadOnlyList<Overlay> Published)
    {
        public MatchInfo Info => Match.Info;
    }
}
