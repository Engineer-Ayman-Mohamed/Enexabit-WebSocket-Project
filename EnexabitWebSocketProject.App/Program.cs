using System.Text;
using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.Features.Auth;
using EnexabitWebSocketProject.App.Features.Channels;
using EnexabitWebSocketProject.App.Features.Messages;
using EnexabitWebSocketProject.App.Health;
using EnexabitWebSocketProject.App.Features.Admin;
using EnexabitWebSocketProject.App.Features.Notifications;
using EnexabitWebSocketProject.App.Hubs;
using EnexabitWebSocketProject.App.Services;
using EnexabitWebSocketProject.App.Services.Export;
using EnexabitWebSocketProject.App.Services.Jobs;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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
builder.Services.AddScoped<NotificationService>();

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
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Redis backplane disabled: {ex.Message}. Running in-memory mode.");
    }
}

builder.Services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
{
    var connectionString = serviceProvider
        .GetRequiredService<IConfiguration>()
        .GetConnectionString("Redis");

    if (string.IsNullOrEmpty(connectionString))
    {
        serviceProvider.GetRequiredService<ILogger<Program>>()
            .LogWarning("Redis connection string not configured. Running without Redis.");
        return null!;
    }

    try
    {
        var redisConfiguration = ConfigurationOptions.Parse(connectionString);
        redisConfiguration.AbortOnConnectFail = false;
        redisConfiguration.ConnectRetry = 2;
        redisConfiguration.ConnectTimeout = 5000;
        return ConnectionMultiplexer.Connect(redisConfiguration);
    }
    catch (Exception ex)
    {
        serviceProvider.GetRequiredService<ILogger<Program>>()
            .LogError(ex, "Failed to connect to Redis. Running without Redis.");
        return null!;
    }
});

builder.Services.AddSingleton<IDatabase>(serviceProvider =>
{
    var multiplexer = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
    return multiplexer?.GetDatabase()!;
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
                    context.HttpContext.Request.Path.StartsWithSegments("/channelHub") ||
                    context.HttpContext.Request.Path.StartsWithSegments("/notificationHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;  
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var authHeader = context.HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                logger.LogError("JWT FAILED: {Error} | Auth header present: {HasHeader} | Header value (first 80 chars): {Header}",
                    context.Exception.Message,
                    !string.IsNullOrEmpty(authHeader),
                    authHeader?.Substring(0, Math.Min(80, authHeader?.Length ?? 0)) ?? "(empty)");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var roles = context.Principal?.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value);
                logger.LogInformation("JWT VALIDATED OK. Roles: {Roles}", string.Join(",", roles ?? []));
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
        Description = "JWT token. Enter only the token (no Bearer prefix needed)"
    });

    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", doc)] = []
    });
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireRole("admin");
    });
    
    options.AddPolicy("UserOrAdmin", policy =>
    {
        policy.RequireRole("user", "admin");
    });
});

var sqlConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

builder.Services.AddHealthChecks()
    .AddSqlServer(
        sqlConnectionString!,
        name: "sqlserver",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db", "sql"])
    .AddRedis(
        redisConnectionString ?? "localhost:6379",
        name: "redis",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["cache", "redis"])
    .AddCheck<SignalRHealthCheck>(
        "signalr",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["realtime"]);

builder.Services.AddHangfire(configuration: config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180);
    config.UseSimpleAssemblyNameTypeSerializer();
    config.UseRecommendedSerializerSettings();
    config.UseSqlServerStorage(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true,
            PrepareSchemaIfNecessary = true,
            SchemaName = "HangFire"
        }
    );
});

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2;
    options.Queues = ["default"];
    options.ServerName = "ExcelExport";
});

builder.Services.AddSingleton<ExportJobStore>();
builder.Services.AddScoped<ImportExportService>();
builder.Services.AddScoped<HangfireExportJobHandler>();

builder.Services.AddAntiforgery();
var app = builder.Build();

await MigrateDatabaseWithRetryAsync(app);


app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    DashboardTitle = "Excel Export Jobs",
    IsReadOnlyFunc = _ => false,
    Authorization = [new HangfireCustomFilter(app.Services)]
});

HangfireRecurringJobs.Register();


app.Use(async (context, next) =>
{
    var origin = $"{context.Request.Scheme}://{context.Request.Host}";
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' https://cdnjs.cloudflare.com 'unsafe-inline' 'wasm-unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self' " +
        $"ws://localhost:5253 wss://localhost:5253 " +
        $"ws://localhost:5173 wss://localhost:5173 " +
        $"ws://localhost:3000 wss://localhost:3000 " +
        $"wss://enexabitwebsocket.runasp.net " +
        $"wss://channel-chat-two.vercel.app; " +
        "worker-src 'self'; " +
        "frame-src 'self'");
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("WebApp");

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/admin"))
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        var scheme = context.Request.Scheme;
        var host = context.Request.Host.Value;
        logger.LogWarning("[DIAG] {Method} {Path} | Scheme: {Scheme} | Host: {Host} | Auth header present: {HasHeader} | Auth header (first 80): {Header}",
            context.Request.Method, context.Request.Path, scheme, host,
            !string.IsNullOrEmpty(authHeader),
            authHeader?.Substring(0, Math.Min(80, authHeader?.Length ?? 0)) ?? "(empty)");
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();
app.MapHub<ChannelHub>("/channelHub");
app.MapHub<NotificationHub>("notificationHub");

AuthEndpoints.Map(app.MapGroup("/api/auth"));

var api = app.MapGroup("/api").RequireAuthorization();
ChannelEndpoints.Map(api.MapGroup("/channels"));
MessageEndpoints.Map(api.MapGroup("/channels"));
NotificationsEndpoints.Map(api.MapGroup("/notifications"));

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckJsonResponseWriter.WriteResponse
}).RequireAuthorization();

app.MapGet("/api/health", async (HealthCheckService healthCheck) =>
{
    var report = await healthCheck.CheckHealthAsync();
    return Results.Ok(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            duration = e.Value.Duration.TotalMilliseconds
        })
    });
})
.WithName("HealthCheck")
.WithTags("Health")
.Produces<object>()
.ProducesProblem(503)
.RequireAuthorization();
AdminEndpoints
    .Map(app.MapGroup("/api/admin")
        .RequireAuthorization("AdminOnly"));

AdminNotificationsEndpoints
    .Map(app.MapGroup("/api/admin")
        .RequireAuthorization("AdminOnly"));

ImportExportEndpoints
    .Map(app.MapGroup("/api/admin/import-export")
        .RequireAuthorization("AdminOnly"));

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
            logger.LogCritical(ex, "Database migration failed after {MaxAttempts} attempts. " +
                "Application will start but database operations will fail.", maxAttempts);
            return;
        }
    }
}