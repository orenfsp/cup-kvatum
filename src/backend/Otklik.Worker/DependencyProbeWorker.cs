using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Otklik.Worker;

public sealed class DependencyProbeWorker(
    HealthCheckService healthChecks,
    ILogger<DependencyProbeWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(30);
    private const string ReadyFile = "/tmp/worker-ready";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var report = await healthChecks.CheckHealthAsync(
                registration => registration.Tags.Contains("infrastructure"),
                stoppingToken);

            if (report.Status == HealthStatus.Healthy)
            {
                await File.WriteAllTextAsync(ReadyFile, DateTimeOffset.UtcNow.ToString("O"), stoppingToken);
                logger.LogInformation(
                    "Dependency probe succeeded for PostgreSQL, Redis and Kafka in {DurationMs} ms",
                    Math.Round(report.TotalDuration.TotalMilliseconds, 1));
            }
            else
            {
                if (File.Exists(ReadyFile))
                {
                    File.Delete(ReadyFile);
                }

                logger.LogWarning(
                    "Dependency probe is unhealthy: {Status}",
                    report.Status);
            }

            await Task.Delay(ProbeInterval, stoppingToken);
        }
    }
}

