using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Otklik.Infrastructure.Security;

public sealed class TrackLookupRateLimiter
{
    public const int AllowedFailures = 5;
    public const int WindowSeconds = 60;
    private const string Prefix = "otklik:track-attempt:";
    private const string BeforeAttemptScript = """
        local count = tonumber(redis.call('GET', KEYS[1]) or '0')
        if count < tonumber(ARGV[1]) then
          return {1, count, 0}
        end
        count = redis.call('INCR', KEYS[1])
        local ttl = redis.call('TTL', KEYS[1])
        if ttl < 1 then
          redis.call('EXPIRE', KEYS[1], ARGV[2])
          ttl = tonumber(ARGV[2])
        end
        local delay = math.min(ttl, math.min(tonumber(ARGV[3]), math.pow(2, count - tonumber(ARGV[1]))))
        return {0, count, delay}
        """;
    private const string RecordFailureScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then redis.call('EXPIRE', KEYS[1], ARGV[1]) end
        return count
        """;

    private readonly IDatabase redis;
    private readonly byte[] hashKey;

    public TrackLookupRateLimiter(IConnectionMultiplexer connection, IConfiguration configuration)
    {
        redis = connection.GetDatabase();
        var configuredKey = configuration["Security:RateLimitHashKey"]
            ?? throw new InvalidOperationException("Security:RateLimitHashKey is required.");
        try
        {
            hashKey = Convert.FromBase64String(configuredKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Security:RateLimitHashKey must be Base64.", exception);
        }
        if (hashKey.Length < 32)
        {
            throw new InvalidOperationException("Security:RateLimitHashKey must contain at least 32 bytes.");
        }
    }

    public async Task<TrackAttemptDecision> CheckBeforeAttemptAsync(
        string clientAddress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = (RedisResult[]?)await redis.ScriptEvaluateAsync(
            BeforeAttemptScript,
            [Key(clientAddress)],
            [AllowedFailures, WindowSeconds, WindowSeconds]);
        if (result is null || result.Length != 3) throw new InvalidOperationException("Unexpected Redis rate-limit result.");
        return new TrackAttemptDecision(
            (long)result[0] == 1,
            checked((int)(long)result[1]),
            checked((int)(long)result[2]));
    }

    public async Task<int> RecordFailureAsync(string clientAddress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await redis.ScriptEvaluateAsync(
            RecordFailureScript,
            [Key(clientAddress)],
            [WindowSeconds]);
        return checked((int)(long)result);
    }

    public string GetAddressDigest(string clientAddress) => Convert.ToHexString(
        HMACSHA256.HashData(hashKey, Encoding.UTF8.GetBytes(clientAddress)));

    private RedisKey Key(string clientAddress) => $"{Prefix}{GetAddressDigest(clientAddress)}";
}

public sealed record TrackAttemptDecision(bool IsAllowed, int FailureCount, int RetryAfterSeconds);
