using System.Data.Common;
using Azure.AI.OpenAI;
using MatchDirector.Agents;
using MatchDirector.Agents.Narration;
using MatchDirector.Agents.Studio;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureEventHubConsumerClient("agents-group");
builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource(Director.Activity.Name, "*Microsoft.Agents.AI*"));

// The studio's agents run on two Foundry deployments: "fast" (live narration) and "reasoning" (review and analysis).
// Without them the director still runs, using deterministic template narration only.
var fast = Deployment("fast");
var reasoning = Deployment("reasoning");

if (fast is not null)
{
    builder.AddKeyedAzureOpenAIClient("fast");
    builder.Services.AddSingleton<INarrator>(sp => new AgentNarrator(
        ChatClient(sp, "fast", fast).AsAIAgent(instructions: AgentNarrator.Instructions, name: "Narrator"),
        sp.GetRequiredService<ILogger<AgentNarrator>>()));
}
else
{
    builder.Services.AddSingleton<INarrator, TemplateNarrator>();
}

if (fast is not null && reasoning is not null)
{
    builder.AddKeyedAzureOpenAIClient("reasoning");
    builder.Services.AddSingleton(sp => new StudioTeam(
        new VerifierCritic(
            ChatClient(sp, "reasoning", reasoning).AsAIAgent(new ChatClientAgentOptions
            {
                Name = "VerifierCritic",
                ChatOptions = new ChatOptions
                {
                    Instructions = VerifierCritic.Instructions,
                    Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low }
                }
            }),
            sp.GetRequiredService<ILogger<VerifierCritic>>()),
        new TacticalAnalyst(ChatClient(sp, "reasoning", reasoning), sp.GetRequiredService<ILogger<TacticalAnalyst>>()),
        builder.Configuration["Studio:FastModel"] ?? Model("fast"),
        builder.Configuration["Studio:ReasoningModel"] ?? Model("reasoning")));
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
