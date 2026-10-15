using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MatchDirector.OverlayHub;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
// Payloads are passed through as the JSON the agent service produced (contract types, camelCase).
builder.Services.AddSignalR();
builder.Services.AddSingleton<BroadcastState>();
builder.Services.AddSingleton<Demand>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseStaticFiles(); // the React app, copied into wwwroot when published
app.MapHub<MatchHub>("/hubs/match");

// Current state for viewers who join mid-match.
app.MapGet("/api/state", (BroadcastState state, Demand demand) => state.Current(demand.Status));

// A viewer asks for a new match. At most one request is pending, so repeated presses cost nothing extra.
app.MapPost("/api/kickoff", (Demand demand) =>
{
    demand.RequestKickoff();
    return Results.Accepted();
});

// Ingest from the agent service only: requires the shared key that the AppHost generates.
var ingestKey = Encoding.UTF8.GetBytes(app.Configuration["Overlay:IngestKey"] ?? "");
var ingest = app.MapGroup("/api").AddEndpointFilter(async (context, next) =>
{
    var supplied = Encoding.UTF8.GetBytes(context.HttpContext.Request.Headers["X-Ingest-Key"].ToString());
    return ingestKey.Length > 0 && CryptographicOperations.FixedTimeEquals(supplied, ingestKey)
        ? await next(context)
        : Results.Unauthorized();
});

ingest.MapPost("/match-info", async (JsonElement info, BroadcastState state, IHubContext<MatchHub> hub) =>
{
    state.Start(info);
    await hub.Clients.All.SendAsync("matchInfo", info);
    return Results.Accepted();
});

ingest.MapPost("/snapshots", async (JsonElement snapshot, BroadcastState state, IHubContext<MatchHub> hub) =>
{
    state.Snapshot = snapshot;
    await hub.Clients.All.SendAsync("snapshot", snapshot);
    return Results.Accepted();
});

// The simulator polls for demand and reports whether a match is playing.
ingest.MapPost("/demand/claim", (Demand demand) => new { claimed = demand.TryClaimKickoff(), viewers = demand.Viewers });
ingest.MapGet("/demand", (Demand demand) => new { viewers = demand.Viewers });
ingest.MapPost("/status", async (StatusReport report, Demand demand, IHubContext<MatchHub> hub) =>
{
    demand.Report(report.Status);
    await hub.Clients.All.SendAsync("status", new { status = demand.Status });
    return Results.Accepted();
});

ingest.MapPost("/overlays", async (JsonElement overlay, BroadcastState state, IHubContext<MatchHub> hub) =>
{
    state.Upsert(overlay);
    await hub.Clients.All.SendAsync("overlay", overlay);
    return Results.Accepted();
});

app.MapFallbackToFile("index.html");

app.Run();

internal sealed record StatusReport(string Status);
