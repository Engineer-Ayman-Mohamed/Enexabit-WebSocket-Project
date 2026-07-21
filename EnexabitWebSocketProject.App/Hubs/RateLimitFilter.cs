using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace EnexabitWebSocketProject.App.Hubs;

public class RateLimitFilter : IHubFilter
{
    private static readonly ConcurrentDictionary<string, LinkedList<long>> _messageTimestamps = new();
    private const int MaxMessagesPerMinute = 30;

    private readonly IHubContext<ChannelHub> _hubContext;

    public RateLimitFilter(IHubContext<ChannelHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (invocationContext.HubMethodName == "SendMessage")
        {
            var connectionId = invocationContext.Context.ConnectionId;
            var timestamps = _messageTimestamps.GetOrAdd(connectionId, _ => new());
            var now = Environment.TickCount64;

            lock (timestamps)
            {
                timestamps.AddLast(now);
                while (timestamps.Count > 0 && (now - timestamps.First!.Value) > 60_000)
                    timestamps.RemoveFirst();

                if (timestamps.Count > MaxMessagesPerMinute)
                {
                    _ = _hubContext.Clients.Client(connectionId)
                        .SendAsync("Error", "Rate limit exceeded. Max 30 messages per minute.");
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
