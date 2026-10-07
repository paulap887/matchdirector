var builder = DistributedApplication.CreateBuilder(args);

var overlayHub = builder.AddProject<Projects.MatchDirector_OverlayHub>("overlayhub")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.MatchDirector_Simulator>("simulator");

builder.AddProject<Projects.MatchDirector_Agents>("agents")
    .WithReference(overlayHub);

builder.AddViteApp("web", "../web")
    .WithReference(overlayHub)
    .WaitFor(overlayHub)
    .WithExternalHttpEndpoints();

builder.Build().Run();
