using System.Security.Claims;
using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.DTOs;
using EnexabitWebSocketProject.App.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EnexabitWebSocketProject.App.Features.Messages;

public static class MessageEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{channelId:int}/messages", GetMessages).RequireAuthorization();
        group.MapPost("/{channelId:int}/messages", SendMessage).RequireAuthorization();
    }

    private static async Task<IResult> GetMessages(int channelId, MessageServices msgService)
    {
        var messages = await msgService.GetRecentMessagesAsync(channelId);
        return Results.Ok(messages);
    }
    
    private static async Task<IResult> SendMessage(
        int channelId, SendMessageRequest req, HttpContext ctx,
        MessageServices msgService, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
            return Results.BadRequest(new { error = "Text is required" });

        if (!await db.Channels.AnyAsync(c => c.Id == channelId))
            return Results.NotFound(new { error = "Channel not found" });

        var displayName = ctx.User.FindFirst("displayName")?.Value ?? "Unknown";
        var message = await msgService.SaveMessageAsync(channelId, displayName, req.Text);
        if (message is null)
            return Results.NotFound(new { error = "Channel not found" });

        return Results.Created($"/api/channels/{channelId}/messages/{message.Id}", new
        {
            message.Id,
            message.UserName,
            message.Text,
            message.CreatedAt
        });
    }
}
