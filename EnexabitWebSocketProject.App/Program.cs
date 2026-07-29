using System.Security.Authentication;
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
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<MessageServices>();

var signalR = builder.Services.AddSignalR(options =>
{
    options.AddFilter<RateLimitFilter>();
});

var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    try
    {
        signalR.AddStackExchangeRedis(redisConnection, options =>
        {
            options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("Enexabit");
            options.Configuration.AbortOnConnectFail = false;
            options.Configuration.Ssl = true;
            options.Configuration.SslProtocols = SslProtocols.Tls12;
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Redis backplane disabled: {ex.Message}. Running in-memory mode.");
    }
}

builder.Services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
{
    var configurationService = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configurationService.GetConnectionString("Redis");
    if (!string.IsNullOrEmpty(connectionString))
    {
        serviceProvider.GetRequiredKeyedService<ILogger<Program>>(null)
            .LogWarning("Redis connection faild, running with no redis");
        return null!;
    }
    try
    {
        var redisConfiguration = ConfigurationOptions.Parse(connectionString!);
        redisConfiguration.AbortOnConnectFail = false;
        redisConfiguration.ConnectRetry = 2;
        redisConfiguration.ConnectTimeout = 5000;
        redisConfiguration.Ssl = true;
        redisConfiguration.SslProtocols = SslProtocols.Tls12;
        return ConnectionMultiplexer.Connect(redisConfiguration);
    }
    catch (Exception ex)
    {
        serviceProvider.GetRequiredService<ILogger<Program>>()
            .LogError(ex, "Failed to connect to Redis. Running without Redis.");
        return null!;
    }
});

builder.Services.AddSingleton(serviceProvider =>
{
    var connectionMultiplexer = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
    if (connectionMultiplexer != null!)
    {
        return connectionMultiplexer.GetDatabase();
    }
    return null!;
});

builder.Services.AddSingleton<RateLimitFilter>(serviceProvider =>
{
    var redis = serviceProvider.GetRequiredService<IDatabase>();
    var logger = serviceProvider.GetRequiredService<ILogger<RateLimitFilter>>();
    return new RateLimitFilter(redis, logger);
});

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
                "http://localhost:3000",
                "http://localhost:5173",
                "https://enexabitwebsocket.runasp.net",
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

await MigrateDatabaseWithRetryAsync(app);

app.Use(async (context, next) =>
{
    var origin = $"{context.Request.Scheme}://{context.Request.Host}";
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
static async Task MigrateDatabaseWithRetryAsync(WebApplication app)
{
    const int maxAttempts = 10;
    var delay = TimeSpan.FromSeconds(6);

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            logger.LogInformation("Applying database migrations (attempt {Attempt}/{MaxAttempts})...", attempt, maxAttempts);
            await db.Database.MigrateAsync();
            await DbInitializer.SeedAsync(db);
            logger.LogInformation("Database migration and seeding completed successfully.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex, "Database not ready yet (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay}s...",
                attempt, maxAttempts, delay.TotalSeconds);
            await Task.Delay(delay);
            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 30));
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Database migration failed after {MaxAttempts} attempts. Application cannot start.", maxAttempts);
            throw;
        }
    }
}