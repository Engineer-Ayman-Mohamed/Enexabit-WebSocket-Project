using System.Text;
using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.Features.Auth;
using EnexabitWebSocketProject.App.Features.Channels;
using EnexabitWebSocketProject.App.Features.Messages;
using EnexabitWebSocketProject.App.Hubs;
using EnexabitWebSocketProject.App.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<MessageServices>();
var signalR = builder.Services.AddSignalR();
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    try
    {
        signalR.AddStackExchangeRedis(redisConnection, options =>
        {
            options.Configuration.ChannelPrefix =
                StackExchange.Redis.RedisChannel.Literal("Enexabit");
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Redis backplane disabled: {ex.Message}. Running in-memory mode.");
    }
}
builder.Services.AddSingleton(typeof(IHubFilter), typeof(RateLimitFilter));

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

app.Use(async (context, next) =>
{
    var origin = $"{context.Request.Scheme}://{context.Request.Host}";
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' https://cdnjs.cloudflare.com 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        $"connect-src 'self' {origin.Replace("http", "ws")} {origin.Replace("http", "wss")} wss://enexabitwebsocket.runasp.net");
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("WebApp");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();
app.MapHub<ChannelHub>("/channelHub");

AuthEndpoints.Map(app.MapGroup("/api/auth"));

var api = app.MapGroup("/api").RequireAuthorization();
ChannelEndpoints.Map(api.MapGroup("/channels"));
MessageEndpoints.Map(api.MapGroup("/channels"));

app.Run();