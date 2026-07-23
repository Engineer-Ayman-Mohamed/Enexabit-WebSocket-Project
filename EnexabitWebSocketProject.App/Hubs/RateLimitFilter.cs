using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StackExchange.Redis;

namespace EnexabitWebSocketProject.App.Hubs;

public class RateLimitFilter : IHubFilter
{
    private readonly IDatabase _redis;
    private readonly ILogger<RateLimitFilter> _logger;
    private const string RateLimitPrefix = "ratelimit:";
    private const int MaxRequestsPerMinute = 30;
    private const int WindowMs = 60_000;
    private const int KeyTtlSeconds = 70;

    private const string RateLimitScript = @"
        local key = KEYS[1]
        local now = tonumber(ARGV[1])
        local windowStart = tonumber(ARGV[2])
        local maxRequests = tonumber(ARGV[3])
        local ttl = tonumber(ARGV[4])
        local member = ARGV[5]
        
        redis.call('ZREMRANGEBYSCORE', key, 0, windowStart)
        local count = redis.call('ZCARD', key)
        
        if count >= maxRequests then
            return 0
        end
        
        redis.call('ZADD', key, now, member)
        redis.call('EXPIRE', key, ttl)
        return 1
    ";

    public RateLimitFilter(IDatabase redis, ILogger<RateLimitFilter> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (invocationContext.HubMethodName == "SendMessage")
        {
            _logger.LogInformation("RateLimitFilter triggered for {ConnectionId}", invocationContext.Context.ConnectionId);
            
            var connectionId = invocationContext.Context.ConnectionId;
            var key = RateLimitPrefix + connectionId;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var windowStart = now - WindowMs;
            var member = $"{now}:{Guid.NewGuid():N}";

            try
            {
                var result = await _redis.ScriptEvaluateAsync(
                    RateLimitScript,
                    new RedisKey[] { key },
                    new RedisValue[] { now,
                        windowStart,
                        MaxRequestsPerMinute,
                        KeyTtlSeconds,
                        member
                    }
                );

                var allowed = result.IsNull ? 0 : (int)result;

                _logger.LogInformation("Rate limit check for {ConnectionId}: allowed={Allowed}, key={Key}", connectionId, allowed, key);

                if (allowed == 0)
                {
                    var hubContext = invocationContext.ServiceProvider.GetRequiredService<IHubContext<ChannelHub>>();
                    await hubContext.Clients.Client(connectionId)
                        .SendAsync("Error", "Rate limit exceeded. Max 30 messages per minute.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rate limit Redis error for {ConnectionId}. Failing open.", connectionId);
            }
        }
        return await next(invocationContext);
    }
}