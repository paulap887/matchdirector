using Aspire.Hosting.Azure;
using Aspire.Hosting.Foundry;
using Azure.Provisioning.AppContainers;

var builder = DistributedApplication.CreateBuilder(args);

// Deploys to Azure Container Apps (the Foundry project is also a compute environment, so services name theirs). Locally everything runs as processes plus the Event Hubs emulator.
// The in-environment Aspire dashboard is a preview component that failed to provision; telemetry goes to App Insights instead.
var aca = builder.AddAzureContainerAppEnvironment("aca")
    .WithDashboard(false);

// Locally this runs the Event Hubs emulator in Docker; azd provisions a real namespace in Azure.
var eventHubs = builder.AddAzureEventHubs("eventhubs")
    .RunAsEmulator();
var matchEvents = eventHubs.AddHub("match-events");
var agentsGroup = matchEvents.AddConsumerGroup("agents-group", "agents");

// Microsoft Foundry: a project for the agent team and two models.
// "reasoning" (gpt-5-mini) analyses and verifies; "fast" (gpt-4.1-mini, no reasoning step) narrates and localises on the live path.
var foundry = builder.AddFoundry("foundry");
var foundryProject = foundry.AddProject("matchdirector");
var reasoningModel = foundry.AddDeployment("reasoning", FoundryModel.OpenAI.Gpt5Mini)
    .WithProperties(d => d.SkuCapacity = 50);
var fastModel = foundry.AddDeployment("fast", "gpt-4.1-mini", "2025-04-14", "OpenAI")
    .WithProperties(d => d.SkuCapacity = 50);

// Application Insights for every service's traces, metrics and logs, including agent and model-call spans.
var insights = builder.AddAzureApplicationInsights("insights");

// Cost guardrail: monthly budget on the resource group with email alerts.
builder.AddBicepTemplate("budget", "../../infra/budget.bicep")
    .WithParameter("contactEmail", builder.AddParameter("budgetEmail"))
    .WithParameter("amount", 30);

// Shared secret so only the agent service can push overlays into the public hub.
var ingestKey = builder.AddParameter("overlayIngestKey", new GenerateParameterDefault { MinLength = 32, Special = false }, secret: true);

// One replica each: the hub keeps viewer state in memory and the director keeps one analyzer per match.
var overlayHub = builder.AddProject<Projects.MatchDirector_OverlayHub>("overlayhub")
    .WithReference(insights)
    .WithEnvironment("Overlay__IngestKey", ingestKey)
    .WithExternalHttpEndpoints()
    .WithComputeEnvironment(aca)
    .PublishAsAzureContainerApp((_, app) => SingleSmallReplica(app));

builder.AddProject<Projects.MatchDirector_Simulator>("simulator")
    .WithReference(insights)
    .WithReference(matchEvents)
    .WithReference(overlayHub)                              // matches play on demand, asked of the hub
    .WithEnvironment("Overlay__IngestKey", ingestKey)
    .WaitFor(matchEvents)
    .WithComputeEnvironment(aca)
    .PublishAsAzureContainerApp((_, app) => SingleSmallReplica(app));

builder.AddProject<Projects.MatchDirector_Agents>("agents")
    .WithReference(insights)
    .WithReference(agentsGroup)
    .WithReference(foundryProject)
    .WithReference(reasoningModel)
    .WithReference(fastModel)
    .WithEnvironment("Studio__FastModel", "gpt-4.1-mini")      // shown in each overlay's agent trace
    .WithEnvironment("Studio__ReasoningModel", "gpt-5-mini")
    .WithReference(overlayHub)
    .WithEnvironment("Overlay__IngestKey", ingestKey)
    .WaitFor(matchEvents)
    .WithComputeEnvironment(aca)
    .PublishAsAzureContainerApp((_, app) => SingleSmallReplica(app));

var web = builder.AddViteApp("web", "../web")
    .WithReference(overlayHub)
    .WaitFor(overlayHub)
    .WithExternalHttpEndpoints()
    .WithComputeEnvironment(aca);

// In Azure the overlay hub serves the built React app, so viewers get one same-origin URL.
overlayHub.PublishWithContainerFiles(web, "./wwwroot");

builder.Build().Run();

static void SingleSmallReplica(ContainerApp app)
{
    app.Template.Scale.MinReplicas = 1;
    app.Template.Scale.MaxReplicas = 1;
    var container = app.Template.Containers.Single().Value!;
    container.Resources.Cpu = 0.25;
    container.Resources.Memory = "0.5Gi";
}
