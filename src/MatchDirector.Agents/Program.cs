using System.Data.Common;
using Azure.AI.OpenAI;
using MatchDirector.Agents;
using MatchDirector.Agents.Narration;
using Microsoft.Agents.AI;
using OpenAI.Chat;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureEventHubConsumerClient("agents-group");
builder.Services.AddOpenTelemetry().WithTracing(t => t.AddSource(Director.Activity.Name, "*Microsoft.Agents.AI*"));

// Narrator agent on the "fast" Foundry deployment when configured; deterministic templates otherwise.
if (builder.Configuration.GetConnectionString("fast") is { } fast)
{
    var deployment = new DbConnectionStringBuilder { ConnectionString = fast }["Deployment"] as string ?? "fast";
    builder.AddAzureOpenAIClient("fast");
    builder.Services.AddSingleton<AIAgent>(sp => sp.GetRequiredService<AzureOpenAIClient>()
        .GetChatClient(deployment)
        .AsAIAgent(instructions: AgentNarrator.Instructions, name: "Narrator"));
    builder.Services.AddSingleton<INarrator, AgentNarrator>();
}
else
{
    builder.Services.AddSingleton<INarrator, TemplateNarrator>();
}

builder.Services.AddHttpClient<OverlayPublisher>(http =>
{
    http.BaseAddress = new Uri("https+http://overlayhub");
    http.DefaultRequestHeaders.Add(OverlayPublisher.IngestKeyHeader, builder.Configuration["Overlay:IngestKey"] ?? "");
});
builder.Services.AddSingleton<Director>();
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
