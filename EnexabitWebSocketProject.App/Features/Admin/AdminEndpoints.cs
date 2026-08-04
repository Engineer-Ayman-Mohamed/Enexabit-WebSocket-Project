using System.Security.Claims;
using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.DTOs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EnexabitWebSocketProject.App.Features.Admin;

public static class AdminEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/users", GetUsers);
        group.MapPut("/users/{userId:int}/role", UpdateUserRole);
    }

    private static async Task<IResult> GetUsers(AppDbContext db)
    {
        var users = await db.Users
            .OrderBy(u => u.Id)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.DisplayName,
                u.Role,
                u.CreatedAt
            })
            .ToListAsync();

        return Results.Ok(users);
    }

    private static async Task<IResult> UpdateUserRole(
        int userId, UpdateRoleRequest req, AppDbContext db, HttpContext ctx)
    {
        if (string.IsNullOrWhiteSpace(req.Role))
            return Results.BadRequest(new { error = "Role is required" });

        var validRoles = new[] { "user", "admin" };
        if (!validRoles.Contains(req.Role))
            return Results.BadRequest(new { error = $"Invalid role. Must be one of: {string.Join(", ", validRoles)}" });

        var user = await db.Users.FindAsync(userId);
        if (user is null)
            return Results.NotFound(new { error = "User not found" });

        var currentUserId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (user.Id.ToString() == currentUserId && req.Role != "admin")
            return Results.BadRequest(new { error = "Cannot demote yourself from admin" });

        user.Role = req.Role;
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = $"User '{user.Username}' role updated to '{user.Role}'",
            user = new { user.Id, user.Username, user.DisplayName, user.Role }
        });
    }
}
