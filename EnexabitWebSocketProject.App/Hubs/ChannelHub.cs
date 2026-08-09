using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnexabitWebSocketProject.App.Data;
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
[Authorize(Roles = "user,admin")]
public class ChannelHub : Hub
{
    private record UserConnection(string DisplayName, HashSet<int> Channels, string ClientType);

    private static readonly ConcurrentDictionary<string, UserConnection> _connections = new();
    private static readonly ConcurrentDictionary<string, string> _clientTypes = new();

    private readonly MessageServices _messageService;
    private readonly NotificationService _notificationService;
    private readonly AppDbContext _context;

    /// <param name="messageService">Service for message persistence and channel validation.</param>
    /// <param name="notificationService">Service for creating notifications.</param>
    /// <param name="context">Database context for channel name lookup.</param>
    public ChannelHub(
        MessageServices messageService,
        NotificationService notificationService,
        AppDbContext context
    ) {
        _messageService = messageService;
        _notificationService = notificationService;
        _context = context;
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
    public async Task JoinChannel(int channelId)
    {
        try
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
        catch (Exception)
        {
            await Clients.Caller.SendAsync("Error", "Failed to join channel. Please try again.");
        }
    }

    /// <summary>
    /// Sends a message to the specified channel.
    /// Triggers mention notifications for @mentioned users.
    /// </summary>
    /// <param name="channelId">The target channel ID.</param>
    /// <param name="text">The message body (empty text is rejected).</param>
    public async Task SendMessage(int channelId, string text)
    {
        try
        {
            if (channelId <= 0)
            {
                await Clients.Caller.SendAsync("Error", "Invalid channel ID");
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                await Clients.Caller.SendAsync("Error", "Message text cannot be empty");
                return;
            }

            if (text.Length > 4000)
            {
                await Clients.Caller.SendAsync("Error", "Message exceeds 4000 character limit");
                return;
            }

            var displayName = Context.User?.FindFirst("displayName")?.Value ?? "Unknown";
            var senderUserId = int.Parse(
                Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");

            var message = await _messageService.SaveMessageAsync(channelId, displayName, text);

            if (message is null)
            {
                await Clients.Caller.SendAsync("Error", "Channel not found");
                return;
            }

            await Clients.Group(channelId.ToString()).SendAsync("NewMessage", new
            {
                message.Id,
                message.UserName,
                message.Text,
                message.CreatedAt
            });

            await ProcessMentionsAsync(channelId, senderUserId, displayName, text);
        }
        catch (Exception)
        {
            await Clients.Caller.SendAsync("Error", "Failed to send message. Please try again.");
        }
    }

    /// <summary>
    /// Shows up to the rest of group which one is currently typing.
    /// </summary>
    /// <param name="channelId">Indicates the current channel.</param>
    public async Task TypingIndicator(int channelId)
    {
        try
        {
            var displayName = Context.User?.FindFirst("displayName")?.Value ?? "Unknown";
            await Clients.OthersInGroup(channelId.ToString())
                .SendAsync("UserTyping", displayName);
        }
        catch (Exception)
        {
            // Typing indicator failed — non-critical
        }
    }

    /// <summary>
    /// Called when a connection disconnects. Removes the connection from tracking,
    /// leaves all joined groups, and broadcasts <c>"UserLeft"</c> to affected channels.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
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
        }
        catch (Exception)
        {
            // Disconnect cleanup failed — non-critical
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Parses message for @mentions, finds users, and creates notifications.
    /// Sends real-time notification via NotificationHub.
    /// </summary>
    private async Task ProcessMentionsAsync(
        int channelId,
        int senderUserId,
        string senderName,
        string messageText
    ) {
        try
        {
            var mentionedUsernames = NotificationService.ExtractMentions(messageText);
            if (mentionedUsernames.Count == 0)
                return;

            var channel = await _context.Channels.FindAsync(channelId);
            var channelName = channel?.Name ?? "unknown";

            var mentionedUsers = await _notificationService
                .FindUsersByUsernamesAsync(mentionedUsernames);

            foreach (var user in mentionedUsers)
            {
                if (user.Id == senderUserId)
                    continue;

                var notification = await _notificationService.MentionNotifyAsync(
                    user.Id, channelId, channelName, senderName, messageText);

                if (notification != null)
                {
                    await Clients.Group($"user_{user.Id}").SendAsync("NewNotification", new
                    {
                        notification.Id,
                        Type = notification.Type.ToString(),
                        notification.Title,
                        notification.Data,
                        notification.IsRead,
                        notification.IsSystemWide,
                        notification.CreatedAt
                    });
                }
            }
        }
        catch (Exception)
        {
            // Mention notifications failed — non-critical, message still sent
        }
    }
}
