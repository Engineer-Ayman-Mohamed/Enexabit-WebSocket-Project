using Hangfire.Dashboard;

namespace EnexabitWebSocketProject.App.Services.Jobs;

/// <summary>
/// Authorization filter for the Hangfire dashboard.
/// Requires the "admin" role to access the dashboard.
/// </summary>
public sealed class HangfireCustomFilter : IDashboardAuthorizationFilter
{
    private readonly IServiceProvider _services;
    public HangfireCustomFilter(IServiceProvider services)
    {
        _services = services;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        
        if (httpContext.User.Identity?.IsAuthenticated != true)
            return false;

        return httpContext.User.IsInRole("admin");
    }
}