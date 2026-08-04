using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.DTOs;
using EnexabitWebSocketProject.App.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EnexabitWebSocketProject.App.Features.Channels;

public static class ChannelEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", GetChannels).RequireAuthorization();
        group.MapPost("/", CreateChannel).RequireAuthorization("AdminOnly");
        group.MapDelete("/{channelId:int}", DeleteChannel).RequireAuthorization("AdminOnly");
    }

    private static async Task<IResult> GetChannels(AppDbContext db)
    {
        var channels = await db.Channels
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();
        return Results.Ok(channels);
    }

    private static async Task<IResult> CreateChannel(CreateChannelRequest req, AppDbContext db)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest(new { error = "Channel name is required" });

        var name = req.Name.Trim().ToLowerInvariant().Replace(" ", "-");

        if (await db.Channels.AnyAsync(c => c.Name == name))
            return Results.Conflict(new { error = $"Channel '{name}' already exists" });

        var channel = new Channel { Name = name };
        db.Channels.Add(channel);
        await db.SaveChangesAsync();

        return Results.Created($"/api/channels/{channel.Id}", new { channel.Id, channel.Name });
    }

    private static async Task<IResult> DeleteChannel(int channelId, AppDbContext db)
    {
        var channel = await db.Channels.FindAsync(channelId);
        if (channel is null)
            return Results.NotFound(new { error = "Channel not found" });

        db.Channels.Remove(channel);
        await db.SaveChangesAsync();

        return Results.Ok(new { message = $"Channel '{channel.Name}' deleted" });
    }
}
