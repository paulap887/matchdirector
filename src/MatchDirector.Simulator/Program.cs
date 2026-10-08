using System.Text.Json;
using MatchDirector.Contracts;
using MatchDirector.Simulator;
using MatchDirector.Simulator.Engine;

// dotnet run -- export <seed> <file.jsonl> writes one full match as JSON lines and exits.
if (args is ["export", var seedArg, var path] && int.TryParse(seedArg, out var exportSeed))
{
    var match = new MatchEngine(exportSeed, Clubs.KestrelBay, Clubs.RedmoorRovers).Play();
    await File.WriteAllLinesAsync(path, match.Events.Select(e => JsonSerializer.Serialize(e, MatchDirectorJson.Options)));
    Console.WriteLine($"Wrote {match.Events.Count} events for {match.MatchId} to {path}");
    return;
}

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.Configure<SimulatorOptions>(builder.Configuration.GetSection(SimulatorOptions.Section));
builder.Services.AddSingleton<IMatchEventSink, LoggingEventSink>();
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
