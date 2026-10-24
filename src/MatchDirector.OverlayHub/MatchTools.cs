using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace MatchDirector.OverlayHub;

/// <summary>
/// MCP tools over the match on air, so any agent (GitHub Copilot in VS Code, for example) can ask about the live match.
/// They are read-only and return what the studio already broadcasts: numbers computed by the stats engine, never by a
/// model, with the event IDs behind each moment.
/// </summary>
[McpServerToolType]
public sealed class MatchTools(BroadcastState state, Demand demand)
{
    private const string Source = "Computed by the Ninetyfold stats engine from synthetic match events. All clubs and players are fictional.";

    [McpServerTool(Name = "get_match_state", Title = "Live match state", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("The match on air: clubs, score, clock, possession, xG, shots, pass accuracy, momentum and the control-vs-chaos index.")]
    public JsonObject GetMatchState()
    {
        if (Info() is not { } info || Snapshot() is not { } snapshot)
            return Idle();

        return new JsonObject
        {
            ["status"] = demand.Status,
            ["matchId"] = Text(info, "matchId"),
            ["venue"] = Text(info, "venue"),
            ["clock"] = Clock(Text(snapshot, "matchClock")),
            ["half"] = snapshot["period"]?.DeepClone(),
            ["home"] = Team(info, snapshot, "home"),
            ["away"] = Team(info, snapshot, "away"),
            ["momentum"] = snapshot["momentum"]?.DeepClone(),
            ["momentumMeaning"] = "-100 to 100; positive means the home club has created most of the recent threat",
            ["chaos"] = snapshot["chaos"]?.DeepClone(),
            ["chaosMeaning"] = "0-100; higher means more end-to-end, lower means more controlled possession",
            ["source"] = Source
        };
    }

    [McpServerTool(Name = "get_recent_moments", Title = "Recent moments", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("The latest moments the studio put on air, newest first: goals, chances, saves, speed alerts, milestones and more. " +
                 "Each has its computed metrics, the analyst's line, the tactical insight if any, and the IDs of the events it was computed from.")]
    public JsonObject GetRecentMoments(
        [Description("How many moments to return, 1-20.")] int count = 8,
        [Description("Optional moment type, e.g. Goal, BigChanceMissed, KeySave, RocketShot, DrivingRun, DistanceMilestone.")] string? type = null)
    {
        if (Info() is not { } info)
            return Idle();

        var moments = new JsonArray();
        foreach (var overlay in state.Overlays().AsEnumerable().Reverse()
                     .Where(o => type is null || string.Equals(o.GetProperty("type").GetString(), type, StringComparison.OrdinalIgnoreCase))
                     .Take(Math.Clamp(count, 1, 20)))
        {
            var o = JsonNode.Parse(overlay.GetRawText())!;
            var variants = o["variants"];
            var player = Player(info, Text(o, "playerId"));
            moments.Add(new JsonObject
            {
                ["clock"] = Clock(Text(o, "matchClock")),
                ["type"] = Text(o, "type"),
                ["player"] = player?["name"]?.DeepClone(),
                ["club"] = player?["club"]?.DeepClone(),
                ["line"] = variants?["analyst"]?.DeepClone(),
                ["insight"] = variants?["insight"]?.DeepClone(),
                ["metrics"] = o["metrics"]?.DeepClone(),
                ["evidenceEventIds"] = o["evidence"]?.DeepClone(),
                ["factChecked"] = o["trace"]?.AsArray().Any(s => Text(s, "agent") == "Verifier critic") == true
            });
        }
        return new JsonObject { ["matchId"] = Text(info, "matchId"), ["moments"] = moments, ["source"] = Source };
    }

    [McpServerTool(Name = "get_player_stats", Title = "Player stats", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("One player's live numbers: passes, progressive passes, shots, goals, xG, distance carried, tackles, interceptions, " +
                 "fastest shot and top carry speed. Find the player by name (or part of it), shirt number or player ID.")]
    public JsonObject GetPlayerStats([Description("Player name, part of a name, shirt number or player ID, e.g. \"Okonkwe\", \"9\" or \"KBY-09\".")] string player)
    {
        if (Info() is not { } info || Snapshot() is not { } snapshot)
            return Idle();

        var matches = Players(info).Where(p =>
            string.Equals(Text(p, "playerId"), player, StringComparison.OrdinalIgnoreCase)
            || Text(p, "name")?.Contains(player, StringComparison.OrdinalIgnoreCase) == true
            || p["number"]?.ToString() == player.TrimStart('#')).ToList();
        if (matches.Count == 0)
            return new JsonObject { ["error"] = $"No player matches \"{player}\".", ["players"] = new JsonArray([.. Players(info).Select(p => (JsonNode?)Text(p, "name"))]) };
        if (matches.Count > 1)
            return new JsonObject { ["error"] = $"Several players match \"{player}\"; be more specific.", ["matches"] = new JsonArray([.. matches.Select(p => (JsonNode?)$"#{p["number"]} {Text(p, "name")} ({Text(p, "club")})")]) };

        var found = matches[0];
        var stats = snapshot["players"]?.AsArray().FirstOrDefault(s => Text(s, "playerId") == Text(found, "playerId"))?.DeepClone();
        return new JsonObject
        {
            ["name"] = Text(found, "name"),
            ["number"] = found["number"]?.DeepClone(),
            ["position"] = Text(found, "position"),
            ["club"] = Text(found, "club"),
            ["stats"] = stats ?? new JsonObject { ["note"] = "No touches yet." },
            ["units"] = "xg: expected goals; distanceCarried: metres; fastestShot and topCarrySpeed: metres per second",
            ["source"] = Source
        };
    }

    [McpServerTool(Name = "get_full_time_report", Title = "Full-time report", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("The full-time match report once the match has ended: headline, the analyst's report, player of the match (chosen by a published formula) and the final stats.")]
    public JsonObject GetFullTimeReport()
    {
        if (state.Recap is not { } recapElement || Info() is not { } info)
            return new JsonObject { ["status"] = demand.Status, ["message"] = "No full-time report yet: the match is still playing or has not started." };

        var recap = JsonNode.Parse(recapElement.GetRawText())!;
        return new JsonObject
        {
            ["matchId"] = Text(recap, "matchId"),
            ["headline"] = Text(recap, "headline"),
            ["report"] = recap["variants"]?["analyst"]?.DeepClone(),
            ["playerOfTheMatch"] = Player(info, Text(recap, "playerOfTheMatch"))?["name"]?.DeepClone(),
            ["metrics"] = recap["metrics"]?.DeepClone(),
            ["evidenceEventIds"] = recap["evidence"]?.DeepClone(),
            ["source"] = Source
        };
    }

    private JsonNode? Info() => state.MatchInfo is { ValueKind: JsonValueKind.Object } info ? JsonNode.Parse(info.GetRawText()) : null;

    private JsonNode? Snapshot() => state.Snapshot is { ValueKind: JsonValueKind.Object } s ? JsonNode.Parse(s.GetRawText()) : null;

    private JsonObject Idle() => new()
    {
        ["status"] = demand.Status,
        ["message"] = "No match on air right now. Open https://overlayhub.whitemeadow-6a3a991d.swedencentral.azurecontainerapps.io to kick one off; it starts within seconds."
    };

    private static JsonObject Team(JsonNode info, JsonNode snapshot, string side)
    {
        var stats = snapshot[side]!;
        return new JsonObject
        {
            ["name"] = Text(info[side], "name"),
            ["goals"] = stats["goals"]?.DeepClone(),
            ["xg"] = stats["xg"]?.DeepClone(),
            ["shots"] = stats["shots"]?.DeepClone(),
            ["shotsOnTarget"] = stats["shotsOnTarget"]?.DeepClone(),
            ["possessionPercent"] = stats["possession"]?.DeepClone(),
            ["passAccuracyPercent"] = stats["passAccuracy"]?.DeepClone(),
            ["progressivePasses"] = stats["progressivePasses"]?.DeepClone(),
            ["tackles"] = stats["tackles"]?.DeepClone(),
            ["interceptions"] = stats["interceptions"]?.DeepClone()
        };
    }

    /// <summary>Every player with their club's name, for lookups.</summary>
    private static IEnumerable<JsonNode> Players(JsonNode info) =>
        new[] { "home", "away" }.SelectMany(side => info[side]?["players"]?.AsArray().OfType<JsonNode>().Select(p =>
        {
            var player = p.DeepClone();
            player["club"] = Text(info[side], "name");
            return player;
        }) ?? []);

    private static JsonNode? Player(JsonNode info, string? playerId) =>
        playerId is null ? null : Players(info).FirstOrDefault(p => Text(p, "playerId") == playerId);

    private static string? Text(JsonNode? node, string key) => node?[key] is JsonValue v && v.TryGetValue(out string? text) ? text : null;

    /// <summary>"01:03:38.5" (a TimeSpan) as the match clock, "63:38".</summary>
    private static string? Clock(string? timeSpan) =>
        TimeSpan.TryParse(timeSpan, out var t) ? $"{(int)t.TotalMinutes:00}:{t.Seconds:00}" : timeSpan;
}
