using System.Data.Common;
using Azure.AI.OpenAI;
using MatchDirector.Agents;
using MatchDirector.Agents.Narration;
using MatchDirector.Agents.Studio;
using OpenAI.Chat;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureEventHubConsumerClient("agents-group");
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(StudioFactory.TelemetrySource, "*Microsoft.Agents.AI*", "*Microsoft.Extensions.AI*"))
    .WithMetrics(m => m.AddMeter(StudioFactory.TelemetrySource, "*Microsoft.Extensions.AI*"));

// The studio's agents run on two Foundry deployments: "fast" (live narration) and "reasoning" (review and analysis).
// Without them the director still runs, using deterministic template narration only.
var fast = Deployment("fast");
var reasoning = Deployment("reasoning");

if (fast is not null)
{
    builder.AddKeyedAzureOpenAIClient("fast");
    builder.Services.AddSingleton<INarrator>(sp => StudioFactory.Narrator(ChatClient(sp, "fast", fast), sp.GetRequiredService<ILoggerFactory>()));
}
else
{
    builder.Services.AddSingleton<INarrator, TemplateNarrator>();
}

if (fast is not null && reasoning is not null)
{
    builder.AddKeyedAzureOpenAIClient("reasoning");
    builder.Services.AddSingleton(sp => StudioFactory.Team(
        ChatClient(sp, "fast", fast),
        ChatClient(sp, "reasoning", reasoning),
        builder.Configuration["Studio:FastModel"] ?? Model("fast"),
        builder.Configuration["Studio:ReasoningModel"] ?? Model("reasoning"),
        sp.GetRequiredService<ILoggerFactory>()));
}
else
{
    builder.Services.AddSingleton(StudioTeam.TemplatesOnly);
}

builder.Services.AddHttpClient<OverlayPublisher>(http =>
{
    http.BaseAddress = new Uri("https+http://overlayhub");
    http.DefaultRequestHeaders.Add(OverlayPublisher.IngestKeyHeader, builder.Configuration["Overlay:IngestKey"] ?? "");
});
builder.Services.AddSingleton<StudioPipeline>();
builder.Services.AddSingleton<Director>();
builder.Services.AddHostedService<Worker>();

builder.Build().Run();

string? Deployment(string name) => Part(name, "Deployment");

// The model behind a deployment, shown in each overlay's agent trace.
string Model(string name) => Part(name, "Model") ?? Part(name, "Deployment") ?? name;

string? Part(string connection, string key) =>
    builder.Configuration.GetConnectionString(connection) is { } value
    && new DbConnectionStringBuilder { ConnectionString = value }.TryGetValue(key, out var part)
        ? part as string
        : null;

static ChatClient ChatClient(IServiceProvider services, string key, string deployment) =>
    services.GetRequiredKeyedService<AzureOpenAIClient>(key).GetChatClient(deployment);
