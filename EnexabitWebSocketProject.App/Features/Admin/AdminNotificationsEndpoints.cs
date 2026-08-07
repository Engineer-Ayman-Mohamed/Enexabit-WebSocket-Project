using EnexabitWebSocketProject.App.DTOs;
using EnexabitWebSocketProject.App.Hubs;
using EnexabitWebSocketProject.App.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace EnexabitWebSocketProject.App.Features.Admin;

public static class AdminNotificationsEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/notifications/maintenance", CreateMaintenanceNotification);
        group.MapPost("/notifications/announce", CreateAnnouncement);
    }

    private static async Task<IResult> CreateMaintenanceNotification(
       [FromBody] CreateMaintainanceNotificationRequest req,
        NotificationService notificationService,
        IHubContext<NotificationHub> notificationHub
    ) {
        var notification = await notificationService
            .NotifySystemMaintenanceAsync(
            req.Title,
            req.Reason,
            req.ScheduledAt,
            req.Duration,
            req.Severity
        );

        if (notification == null)
            return Results.BadRequest(new { error = "Failed to create maintenance notification" });

        await notificationHub.Clients.All.SendAsync("NewNotification", new
        {
            notification.Id,
            Type = notification.Type.ToString(),
            notification.Title,
            notification.Data,
            notification.IsRead,
            notification.IsSystemWide,
            notification.CreatedAt
        });

        return Results.Created($"/api/notifications/{notification.Id}", new
        {
            notification.Id,
            notification.Title,
            notification.CreatedAt
        });
    }

    private static async Task<IResult> CreateAnnouncement(
        [FromBody] CreateAnnouncementRequest req,
        NotificationService notificationService,
        IHubContext<NotificationHub> notificationHub
    ) {
        var notification = await notificationService
            .NotifySystemAnnouncementAsync(
            req.Message,
            req.Link);

        if (notification == null)
            return Results.BadRequest(new { error = "Failed to create announcement" });

        await notificationHub.Clients.All.SendAsync("NewNotification", new
        {
            notification.Id,
            Type = notification.Type.ToString(),
            notification.Title,
            notification.Data,
            notification.IsRead,
            notification.IsSystemWide,
            notification.CreatedAt
        });

        return Results.Created($"/api/notifications/{notification.Id}", new
        {
            notification.Id,
            notification.Title,
            notification.CreatedAt
        });
    }
}