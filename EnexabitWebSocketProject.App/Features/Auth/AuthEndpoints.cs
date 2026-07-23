using System.Security.Claims;
using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.DTOs;
using EnexabitWebSocketProject.App.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EnexabitWebSocketProject.App.Features.Auth;

public static class AuthEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapPost("/refresh", Refresh);
        group.MapPost("/logout", Logout).RequireAuthorization();
    }

    private static async Task<IResult> Register(RegisterRequest req, AuthService auth)
    {
        var (user, error) = await auth.RegisterAsync(req.Username, req.Password, req.DisplayName);
        if (error is not null)
            return Results.Conflict(new { error });
        return Results.Created($"/users/{user!.Id}", new { user.Username, user.DisplayName });
    }

    private static async Task<IResult> Login(LoginRequest req, AuthService auth, TokenService token, HttpContext ctx)
    {
        var user = await auth.AuthenticateAsync(req.Username, req.Password);
        if (user is null)
            return Results.Unauthorized();

        var clientType = ctx.Request.Headers["X-Client-Type"].FirstOrDefault() ?? "web";
        var accessToken = token.GenerateAccessToken(user);
        var refreshToken = await token.GenerateRefreshTokenAsync(user.Id);

        ctx.Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7)
        });

        return Results.Ok(new LoginResponse
        {
            AccessToken = accessToken,
            DisplayName = user.DisplayName,
            RefreshToken = clientType.Equals("mobile", StringComparison.OrdinalIgnoreCase) ? refreshToken : null
        });
    }

    private static async Task<IResult> Refresh(HttpContext ctx, TokenService token)
    {
        var clientType = ctx.Request.Headers["X-Client-Type"].FirstOrDefault() ?? "web";

        var oldToken = ctx.Request.Cookies["refreshToken"];

        if (string.IsNullOrEmpty(oldToken))
            oldToken = ctx.Request.Headers["X-Refresh-Token"].FirstOrDefault();

        if (string.IsNullOrEmpty(oldToken) && ctx.Request.HasJsonContentType())
        {
            var body = await ctx.Request.ReadFromJsonAsync<RefreshRequest>();
            oldToken = body?.RefreshToken;
        }

        if (string.IsNullOrEmpty(oldToken))
            return Results.Unauthorized();

        var result = await token.RotateRefreshTokenAsync(oldToken);
        if (result is null)
        {
            ctx.Response.Cookies.Delete("refreshToken");
            return Results.Unauthorized();
        }

        var (newAccessToken, newRefreshToken) = result.Value;

        ctx.Response.Cookies.Append("refreshToken", newRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = clientType.Equals("mobile")
                ? SameSiteMode.None
                : SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7)
        });

        if (clientType.Equals("mobile", StringComparison.OrdinalIgnoreCase))
            return Results.Ok(new { accessToken = newAccessToken, refreshToken = newRefreshToken });

        return Results.Ok(new { accessToken = newAccessToken });
    }

    private static async Task<IResult> Logout(HttpContext ctx, TokenService token, AppDbContext db)
    {
        var userIdClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim is null)
            return Results.Unauthorized();

        var userId = int.Parse(userIdClaim);
        await token.RevokeAllUserTokensAsync(userId);

        ctx.Response.Cookies.Delete("refreshToken");

        var refreshToken = ctx.Request.Headers["X-Refresh-Token"].FirstOrDefault();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var stored = await db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == refreshToken && rt.UserId == userId);
            if (stored is not null)
            {
                stored.RevokedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        return Results.Ok(new { message = "Logged out" });
    }
}
