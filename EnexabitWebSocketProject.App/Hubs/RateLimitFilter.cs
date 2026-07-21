using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace EnexabitWebSocketProject.App.Hubs;

public class RateLimitFilter : IHubFilter
{
    private static readonly ConcurrentDictionary<string, LinkedList<DateTime>> _messageTimestamps = new();
    private const int MaxMessagesPerMinute = 30;

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (invocationContext.HubMethodName == "SendMessage")
        {
            var connectionId = invocationContext.Context.ConnectionId;
            var timestamps = _messageTimestamps.GetOrAdd(connectionId, _ => new());
            var now = DateTime.UtcNow;

            lock (timestamps)
            {
                timestamps.AddLast(now);
                while (timestamps.Count > 0 && (now - timestamps.First!.Value).TotalSeconds > 60)
                    timestamps.RemoveFirst();

                if (timestamps.Count > MaxMessagesPerMinute)
                {
                    invocationContext.Context.Abort();
                    return null;
                }
            }
        }

        return await next(invocationContext);
    }
    public static void Cleanup(string connectionId)
    {
        _messageTimestamps.TryRemove(connectionId, out _);
    }
}
