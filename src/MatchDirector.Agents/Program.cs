using MatchDirector.Agents;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureEventHubConsumerClient("agents-group");
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
