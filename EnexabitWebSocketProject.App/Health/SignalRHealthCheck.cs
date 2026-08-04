using EnexabitWebSocketProject.App.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnexabitWebSocketProject.App.Health;

public class SignalRHealthCheck : IHealthCheck
{
    private readonly IHubContext<ChannelHub>? _channelHubContext;
    
    public SignalRHealthCheck(IHubContext<ChannelHub> channelHubContext)
    {
        _channelHubContext = channelHubContext;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    ) {
        if (_channelHubContext != null)
        {
            return Task.FromResult(
                HealthCheckResult.Healthy(
                    "SignalR Is Healthy",
                    new Dictionary<string, object>
                    {
                        ["status"] = "Healthy",
                        ["hub"] = nameof(ChannelHub)
                    }
                )
            );
        }
        return Task.FromResult(
            HealthCheckResult.Unhealthy("SignalR Is Not Healthy")
        );
    }
}