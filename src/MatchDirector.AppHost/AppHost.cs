using Aspire.Hosting.Foundry;

var builder = DistributedApplication.CreateBuilder(args);

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

// Cost guardrail: monthly budget on the resource group with email alerts.
builder.AddBicepTemplate("budget", "../../infra/budget.bicep")
    .WithParameter("contactEmail", builder.AddParameter("budgetEmail"))
    .WithParameter("amount", 30);

var overlayHub = builder.AddProject<Projects.MatchDirector_OverlayHub>("overlayhub")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.MatchDirector_Simulator>("simulator")
    .WithReference(matchEvents)
    .WaitFor(matchEvents);

builder.AddProject<Projects.MatchDirector_Agents>("agents")
    .WithReference(agentsGroup)
    .WithReference(foundryProject)
    .WithReference(reasoningModel)
    .WithReference(fastModel)
    .WithReference(overlayHub)
    .WaitFor(matchEvents);

builder.AddViteApp("web", "../web")
    .WithReference(overlayHub)
    .WaitFor(overlayHub)
    .WithExternalHttpEndpoints();

builder.Build().Run();
