using StackExchange.Redis;

namespace Otklik.Api.Endpoints;

internal enum OperatorWorkState
{
    Available,
    Mine,
    Busy
}

internal static class OperatorWorkLease
{
    public const int LeaseSeconds = 90;
    public const string HeaderName = "X-Operator-Work-Lease";

    public static async Task<bool> TryAcquireAsync(
        IConnectionMultiplexer redis,
        Guid appealId,
        Guid operatorId,
        Guid leaseId)
    {
        const string script = "local c=redis.call('GET',KEYS[1]); if (not c) or c==ARGV[1] then redis.call('SET',KEYS[1],ARGV[1],'PX',ARGV[2]); return 1 else return 0 end";
        return (long)await redis.GetDatabase().ScriptEvaluateAsync(
            script,
            [Key(appealId)],
            [Value(operatorId, leaseId), (RedisValue)(LeaseSeconds * 1000)]) == 1;
    }

    public static async Task<bool> RenewAsync(
        IConnectionMultiplexer redis,
        Guid appealId,
        Guid operatorId,
        Guid leaseId)
    {
        const string script = "if redis.call('GET',KEYS[1])==ARGV[1] then redis.call('PEXPIRE',KEYS[1],ARGV[2]); return 1 else return 0 end";
        return (long)await redis.GetDatabase().ScriptEvaluateAsync(
            script,
            [Key(appealId)],
            [Value(operatorId, leaseId), (RedisValue)(LeaseSeconds * 1000)]) == 1;
    }

    public static async Task ReleaseAsync(
        IConnectionMultiplexer redis,
        Guid appealId,
        Guid operatorId,
        Guid leaseId)
    {
        const string script = "if redis.call('GET',KEYS[1])==ARGV[1] then return redis.call('DEL',KEYS[1]) else return 0 end";
        await redis.GetDatabase().ScriptEvaluateAsync(
            script,
            [Key(appealId)],
            [Value(operatorId, leaseId)]);
    }

    public static async Task<Guid?> TryAcquireFirstAsync(
        IConnectionMultiplexer redis,
        IReadOnlyList<Guid> appealIds,
        Guid operatorId,
        Guid leaseId)
    {
        if (appealIds.Count == 0) return null;
        const string script = "for i,key in ipairs(KEYS) do if not redis.call('GET',key) then redis.call('SET',key,ARGV[1],'PX',ARGV[2]); return i end end return 0";
        var index = (long)await redis.GetDatabase().ScriptEvaluateAsync(
            script,
            appealIds.Select(Key).ToArray(),
            [Value(operatorId, leaseId), (RedisValue)(LeaseSeconds * 1000)]);
        return index > 0 ? appealIds[(int)index - 1] : null;
    }

    public static async Task<IReadOnlyDictionary<Guid, OperatorWorkState>> ReadStatesAsync(
        IConnectionMultiplexer redis,
        IReadOnlyList<Guid> appealIds,
        Guid operatorId,
        Guid? leaseId)
    {
        if (appealIds.Count == 0) return new Dictionary<Guid, OperatorWorkState>();
        var values = await redis.GetDatabase().StringGetAsync(appealIds.Select(Key).ToArray());
        var mine = leaseId is null || leaseId == Guid.Empty ? null : Value(operatorId, leaseId.Value).ToString();
        return appealIds.Select((appealId, index) => new
        {
            appealId,
            state = !values[index].HasValue
                ? OperatorWorkState.Available
                : mine is not null && values[index].ToString() == mine
                    ? OperatorWorkState.Mine
                    : OperatorWorkState.Busy
        }).ToDictionary(item => item.appealId, item => item.state);
    }

    public static async Task<bool> CanWriteAsync(
        IConnectionMultiplexer redis,
        Guid appealId,
        Guid operatorId,
        Guid? leaseId)
    {
        var current = await redis.GetDatabase().StringGetAsync(Key(appealId));
        if (!current.HasValue) return true;
        return leaseId is not null
            && leaseId != Guid.Empty
            && current == Value(operatorId, leaseId.Value);
    }

    public static Guid? ReadLeaseId(HttpRequest request) =>
        Guid.TryParse(request.Headers[HeaderName].FirstOrDefault(), out var leaseId) && leaseId != Guid.Empty
            ? leaseId
            : null;

    private static RedisKey Key(Guid appealId) => $"operator-work:{appealId:N}";
    private static RedisValue Value(Guid operatorId, Guid leaseId) => $"{operatorId:N}:{leaseId:N}";
}
