var builder = DistributedApplication.CreateBuilder(args);

// Locally this runs the Event Hubs emulator in Docker; azd provisions a real namespace in Azure.
var eventHubs = builder.AddAzureEventHubs("eventhubs")
    .RunAsEmulator();
var matchEvents = eventHubs.AddHub("match-events");
var agentsGroup = matchEvents.AddConsumerGroup("agents-group", "agents");

var overlayHub = builder.AddProject<Projects.MatchDirector_OverlayHub>("overlayhub")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.MatchDirector_Simulator>("simulator")
    .WithReference(matchEvents)
    .WaitFor(matchEvents);

builder.AddProject<Projects.MatchDirector_Agents>("agents")
    .WithReference(agentsGroup)
    .WithReference(overlayHub)
    .WaitFor(matchEvents);

builder.AddViteApp("web", "../web")
    .WithReference(overlayHub)
    .WaitFor(overlayHub)
    .WithExternalHttpEndpoints();

builder.Build().Run();
