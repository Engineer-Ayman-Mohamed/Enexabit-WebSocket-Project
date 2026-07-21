using EnexabitWebSocketProject.App.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EnexabitWebSocketProject.App.Features.Channels;

public static class ChannelEndpoints
{
    public static RouteGroupBuilder MapChannelEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (AppDbContext db) =>
        {
            var channels = await db.Channels
                .OrderBy(c => c.Id)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();
            return Results.Ok(channels);
        }).RequireAuthorization();

        return group;
    }
}
