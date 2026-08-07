using System.Security.Claims;
using EnexabitWebSocketProject.App.DTOs;
using EnexabitWebSocketProject.App.Services;
using Microsoft.AspNetCore.Mvc;

namespace EnexabitWebSocketProject.App.Features.Notifications;

public static class NotificationsEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", GetNotifications);
        group.MapGet("/unread-count", GetUnreadCount);
        group.MapPut("/{notificationId:int}/read", MarkAsRead);
        group.MapPut("/{notificationId:int}/acknowledge", AcknowledgeNotification);
        group.MapPut("/read-all", MarkAllAsRead);
        group.MapDelete("/{notificationId:int}", DeleteNotification);
        group.MapGet("/preferences", GetPreferences);
        group.MapPut("/preferences", UpdatePreferences);
    }

    private static async Task<IResult> GetNotifications(
        HttpContext ctx,
        NotificationService notificationService,
        [AsParameters] int page = 1,
        [AsParameters] int pageSize = 20
    ) {
        var userId = GetUserId(ctx);
        if (userId == null)
            return Results.Unauthorized();

        var result = await notificationService
            .GetNotificationsAsync(userId.Value, page, pageSize);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetUnreadCount(
        HttpContext ctx,
        NotificationService notificationService
    ) {
        var userId = GetUserId(ctx);
        if (userId == null)
            return Results.Unauthorized();

        var count = await notificationService
            .GetUnreadCountAsync(userId.Value);
        return Results.Ok(new { count });
    }

    private static async Task<IResult> MarkAsRead(
        int notificationId,
        HttpContext ctx,
        NotificationService notificationService
    ) {
        var userId = GetUserId(ctx);
        if (userId == null)
            return Results.Unauthorized();

        var success = await notificationService
            .MarkAsReadAsync(notificationId, userId.Value);
        if (!success)
            return Results.NotFound(new { error = "Notification not found" });

        return Results.Ok(new { message = "Notification marked as read" });
    }

    private static async Task<IResult> AcknowledgeNotification(
        int notificationId,
        HttpContext ctx,
        NotificationService notificationService
    ) {
        var userId = GetUserId(ctx);
        if (userId == null)
            return Results.Unauthorized();

        var success = await notificationService
            .AcknowledgeNotificationAsync(notificationId, userId.Value);
        if (!success)
            return Results.NotFound(new { error = "Notification not found or not a maintenance notification" });

        return Results.Ok(new { message = "Notification acknowledged" });
    }

    private static async Task<IResult> MarkAllAsRead(
        HttpContext ctx,
        NotificationService notificationService
    ) {
        var userId = GetUserId(ctx);
        if (userId == null)
            return Results.Unauthorized();

        var count = await notificationService
            .MarkAllAsReadAsync(userId.Value);
        return Results.Ok(new { message = $"{count} notifications marked as read" });
    }

    private static async Task<IResult> DeleteNotification(
        int notificationId,
        HttpContext ctx,
        NotificationService notificationService
    ) {
        var userId = GetUserId(ctx);
        if (userId == null)
            return Results.Unauthorized();

        var success = await notificationService
            .DeleteNotificationAsync(notificationId, userId.Value);
        if (!success)
            return Results.NotFound(new { error = "Notification not found" });

        return Results.Ok(new { message = "Notification deleted" });
    }

    private static async Task<IResult> GetPreferences(
        HttpContext ctx,
        NotificationService notificationService
    ) {
        var userId = GetUserId(ctx);
        if (userId == null)
            return Results.Unauthorized();

        var preferences = await notificationService
            .GetPreferencesAsync(userId.Value);
        return Results.Ok(preferences);
    }

    private static async Task<IResult> UpdatePreferences(
        [FromBody] UpdateNotificationPreferencesRequest req,
        HttpContext ctx,
        NotificationService notificationService
    ) {
        var userId = GetUserId(ctx);
        if (userId == null)
            return Results.Unauthorized();

        var success = await notificationService
            .UpdatePreferencesAsync(userId.Value, req.Preferences);
        if (!success)
            return Results.BadRequest(new { error = "Failed to update preferences" });

        return Results.Ok(new { message = "Preferences updated" });
    }

    private static int? GetUserId(HttpContext ctx)
    {
        var userIdClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return null;
        return int.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}