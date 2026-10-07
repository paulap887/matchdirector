namespace MatchDirector.Agents;

public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Agents started");
        return Task.CompletedTask;
    }
}
