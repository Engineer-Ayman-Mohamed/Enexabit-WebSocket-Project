using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.DTOs;
using EnexabitWebSocketProject.App.Hubs;
using EnexabitWebSocketProject.App.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<MessageServices>();
builder.Services.AddSignalR();

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

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebApp", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5253",
                "https://enexabitwebsocket.runasp.net",
                "http://localhost:5173",
                "https://channel-chat-two.vercel.app"
        )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Channel Chat API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", null, null), [] }
    });
});

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbInitializer.SeedAsync(db);
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("WebApp");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' https://cdnjs.cloudflare.com 'unsafe-inline' 'wasm-unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self' ws://localhost:5253 wss://enexabitwebsocket.runasp.net; " +
        "worker-src 'self'; " +
        "frame-src 'self'");
    await next();
});

app.UseSwagger();
app.UseSwaggerUI();
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
});

app.MapPost("/api/auth/refresh", async (HttpContext ctx, TokenService token) =>
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
});

app.MapPost("/api/auth/logout", async (HttpContext ctx, TokenService token, AppDbContext db) =>
{
    var userIdClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
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
}).RequireAuthorization();

app.MapHub<ChannelHub>("/channelHub");

app.MapGet("/api/channels", async (AppDbContext db) =>
{
    var channels = await db.Channels
        .OrderBy(c => c.Id)
        .Select(c => new { c.Id, c.Name })
        .ToListAsync();
    return Results.Ok(channels);
}).RequireAuthorization();

app.MapGet("/api/channels/{channelId:int}/messages", async (int channelId, MessageServices msgService) =>
{
    var messages = await msgService.GetRecentMessagesAsync(channelId);
    return Results.Ok(messages);
}).RequireAuthorization();

app.MapPost("/api/channels/{channelId:int}/messages", async (int channelId, SendMessageRequest req, HttpContext ctx, MessageServices msgService, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest(new { error = "Text is required" });

    if (!await db.Channels.AnyAsync(c => c.Id == channelId))
        return Results.NotFound(new { error = "Channel not found" });

    var displayName = ctx.User.FindFirst("displayName")?.Value ?? "Unknown";
    var message = await msgService.SaveMessageAsync(channelId, displayName, req.Text);

    return Results.Created($"/api/channels/{channelId}/messages/{message.Id}", new
    {
        message.Id,
        message.UserName,
        message.Text,
        message.CreatedAt
    });
}).RequireAuthorization();
app.Run();