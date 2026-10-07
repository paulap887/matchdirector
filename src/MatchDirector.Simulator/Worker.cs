namespace MatchDirector.Simulator;

public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Simulator started");
        return Task.CompletedTask;
    }
}
