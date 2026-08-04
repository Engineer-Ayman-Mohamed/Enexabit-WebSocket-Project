using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnexabitWebSocketProject.App.Health;

public static class HealthCheckJsonResponseWriter
{
    public static async Task WriteResponse(
        HttpContext context,
        HealthReport report
    ) {
        context.Response.ContentType = "application/json";
        var jsonResponse = new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.ToString(),
                description = e.Value.Description,
                exception = e.Value.Exception?.Message,
                tags = e.Value.Tags
            })
        };
        await context.Response.WriteAsJsonAsync(
            jsonResponse,
            new JsonSerializerOptions { WriteIndented = true }
        );
    }
}