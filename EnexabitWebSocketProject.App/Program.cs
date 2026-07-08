using System.Text;
using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.DTOs;
using EnexabitWebSocketProject.App.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/channelHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbInitializer.SeedAsync(db);
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/auth/register", async (RegisterRequest req, AuthService auth) =>
{
    var (user, error) = await auth.RegisterAsync(req.Username, req.Password, req.DisplayName);
    if (error is not null)
        return Results.Conflict(new { error });
    return Results.Created($"/users/{user!.Id}", new { user.Username, user.DisplayName });
});

app.MapPost("/api/auth/login", async (LoginRequest req, AuthService auth, TokenService token, HttpContext ctx) =>
{
    var user = await auth.AuthenticateAsync(req.Username, req.Password);
    if (user is null)
        return Results.Unauthorized();

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
        DisplayName = user.DisplayName
    });
});

app.MapPost("/api/auth/refresh", async (HttpContext ctx, TokenService token) =>
{
    var oldToken = ctx.Request.Cookies["refreshToken"];
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
        SameSite = SameSiteMode.Strict,
        Expires = DateTime.UtcNow.AddDays(7)
    });

    return Results.Ok(new { accessToken = newAccessToken });
});

app.MapPost("/api/auth/logout", async (HttpContext ctx, TokenService token) =>
{
    var userIdClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (userIdClaim is null)
        return Results.Unauthorized();

    await token.RevokeAllUserTokensAsync(int.Parse(userIdClaim));
    ctx.Response.Cookies.Delete("refreshToken");
    return Results.Ok(new { message = "Logged out" });
}).RequireAuthorization();

app.Run();