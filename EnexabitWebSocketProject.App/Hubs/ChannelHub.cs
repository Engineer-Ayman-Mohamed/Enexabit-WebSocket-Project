using System.Collections.Concurrent;
using EnexabitWebSocketProject.App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EnexabitWebSocketProject.App.Hubs;

[Authorize]
public class ChannelHub : Hub
{
    private record UserConnection(string DisplayName, HashSet<int> Channels);

    private static readonly ConcurrentDictionary<string, UserConnection> _connections = new();

    private readonly MessageServices _messageService;

    public ChannelHub(MessageServices messageService)
    {
        _messageService = messageService;
    }

    public async Task JoinChannel(int channelId)
    {
        if (!await _messageService.ChannelExistsAsync(channelId))
        {
            await Clients.Caller.SendAsync("Error", "Channel not found");
            return;
        }

        var connectionId = Context.ConnectionId;
        var displayName = Context.User?.FindFirst("displayName")?.Value ?? "Unknown";

        _connections.AddOrUpdate(connectionId,
            _ => new UserConnection(displayName, [channelId]),
            (_, uc) => { uc.Channels.Add(channelId); return uc; });

        await Groups.AddToGroupAsync(connectionId, channelId.ToString());

        var recentMessages = await _messageService.GetRecentMessagesAsync(channelId);
        await Clients.Caller.SendAsync("JoinedChannel", recentMessages);
        await Clients.OthersInGroup(channelId.ToString()).SendAsync("UserJoined", displayName);
    }

    public async Task SendMessage(int channelId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            await Clients.Caller.SendAsync("Error", "Message text cannot be empty");
            return;
        }

        if (!await _messageService.ChannelExistsAsync(channelId))
        {
            await Clients.Caller.SendAsync("Error", "Channel not found");
            return;
        }

        var displayName = Context.User?.FindFirst("displayName")?.Value ?? "Unknown";
        var message = await _messageService.SaveMessageAsync(channelId, displayName, text);

        await Clients.Group(channelId.ToString()).SendAsync("NewMessage", new
        {
            message.Id,
            message.UserName,
            message.Text,
            message.CreatedAt
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        if (_connections.TryRemove(connectionId, out var userConnection))
        {
            foreach (var channelId in userConnection.Channels)
            {
                await Groups.RemoveFromGroupAsync(connectionId, channelId.ToString());
                await Clients.Group(channelId.ToString()).SendAsync("UserLeft", userConnection.DisplayName);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
