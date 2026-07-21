using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnexabitWebSocketProject.App.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EnexabitWebSocketProject.App.Hubs;

/// <summary>
/// SignalR hub for real-time channel-based chat.
/// Requires JWT authentication via the <c>[Authorize]</c> attribute.
/// Connections are tracked in a <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// to enable reverse lookup (connection → channels) on disconnect.
/// </summary>
[Authorize]
public class ChannelHub : Hub
{
    private record UserConnection(string DisplayName, HashSet<int> Channels, string ClientType);

    private static readonly ConcurrentDictionary<string, UserConnection> _connections = new();
    private static readonly ConcurrentDictionary<string, string> _clientTypes = new();

    private readonly MessageServices _messageService;

    /// <param name="messageService">Service for message persistence and channel validation.</param>
    public ChannelHub(MessageServices messageService)
    {
        _messageService = messageService;
    }

    /// <summary>
    /// Called when a new connection is established.
    /// Reads the <c>X-Client-Type</c> header to determine the client device type (mobile/web).
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var clientType = httpContext?.Request.Headers["X-Client-Type"].FirstOrDefault() ?? "web";
        _clientTypes[Context.ConnectionId] = clientType;

        Console.WriteLine($"[{clientType}] connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Joins a named channel group, loads recent messages, and notifies other members.
    /// </summary>
    /// <param name="channelId">The channel ID to join.</param>
    /// <returns>A task that completes when the channel is joined and history is sent.</returns>
    /// <remarks>
    /// If the channel does not exist, the caller receives an <c>"Error"</c> event.
    /// On success, the caller receives a <c>"JoinedChannel"</c> event with message history,
    /// and other group members receive a <c>"UserJoined"</c> event.
    /// </remarks>
    public async Task JoinChannel(int channelId)
    {
        if (!await _messageService.ChannelExistsAsync(channelId))
        {
            await Clients.Caller.SendAsync("Error", "Channel not found");
            return;
        }

        var connectionId = Context.ConnectionId;
        var displayName = Context.User?.FindFirst("displayName")?.Value ?? "Unknown";
        var clientType = _clientTypes.GetValueOrDefault(connectionId, "web");

        _connections.AddOrUpdate(connectionId,
            _ => new UserConnection(displayName, [channelId], clientType),
            (_, uc) => { uc.Channels.Add(channelId); return uc; });

        await Groups.AddToGroupAsync(connectionId, channelId.ToString());

        var recentMessages = await _messageService.GetRecentMessagesAsync(channelId);
        
        await Clients.Caller.SendAsync("JoinedChannel", recentMessages);
        
        await Clients.OthersInGroup(channelId.ToString()).SendAsync("UserJoined", displayName);
    }

    /// <summary>
    /// Sends a message to the specified channel.
    /// The sender's display name is extracted from the JWT. HTML is stripped for XSS prevention.
    /// </summary>
    /// <param name="channelId">The target channel ID.</param>
    /// <param name="text">The message body (empty text is rejected).</param>
    /// <returns>A task that completes when the message is persisted and broadcast.</returns>
    /// <remarks>
    /// On success, all group members receive a <c>"NewMessage"</c> event.
    /// If validation fails, the caller receives an <c>"Error"</c> event.
    /// </remarks>
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

    /// <summary>
    /// Called when a connection disconnects. Removes the connection from tracking,
    /// leaves all joined groups, and broadcasts <c>"UserLeft"</c> to affected channels.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        _clientTypes.TryRemove(connectionId, out _);

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
