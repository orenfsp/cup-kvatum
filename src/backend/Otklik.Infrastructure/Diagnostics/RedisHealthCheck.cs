using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Otklik.Infrastructure.Diagnostics;

internal sealed class RedisHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await connection.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy(
                "Redis отвечает",
                new Dictionary<string, object> { ["latencyMs"] = latency.TotalMilliseconds });
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Redis недоступен", exception);
        }
    }
}

