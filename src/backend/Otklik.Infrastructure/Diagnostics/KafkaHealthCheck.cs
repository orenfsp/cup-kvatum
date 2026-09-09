using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Otklik.Infrastructure.Diagnostics;

internal sealed class KafkaHealthCheck(IAdminClient adminClient) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(3));
            return Task.FromResult(metadata.Brokers.Count > 0
                ? HealthCheckResult.Healthy(
                    "Kafka отвечает",
                    new Dictionary<string, object> { ["brokers"] = metadata.Brokers.Count })
                : HealthCheckResult.Unhealthy("Kafka не вернула брокеры"));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka недоступна", exception));
        }
    }
}

