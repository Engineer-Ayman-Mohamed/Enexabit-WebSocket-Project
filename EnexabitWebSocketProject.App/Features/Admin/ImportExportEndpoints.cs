using EnexabitWebSocketProject.App.DTOs.Export;
using EnexabitWebSocketProject.App.DTOs.Import;
using EnexabitWebSocketProject.App.Helpers;
using EnexabitWebSocketProject.App.Services;
using EnexabitWebSocketProject.App.Services.Export;
using EnexabitWebSocketProject.App.Services.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace EnexabitWebSocketProject.App.Features.Admin;

public static class ImportExportEndpoints
{
    /// <summary>Maximum upload file size: 10MB. Exports have no size limit.</summary>
    private const long MaxUploadFileSize = 10 * 1024 * 1024;

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/export/users", ExportUsers)
            .WithName("ExportUsers")
            .WithSummary("Download all users as Excel (synchronous — use for small datasets)")
            .Produces<FileResult>();

        group.MapGet("/export/messages", ExportMessages)
            .WithName("ExportMessages")
            .WithSummary("Download all messages as Excel (synchronous — use for small datasets)")
            .Produces<FileResult>();

        group.MapPost("/export/{type}/queue", QueueExport)
            .WithName("QueueExport")
            .WithSummary("Queue an export job for background processing — returns immediately")
            .Produces<ExportJobResponse>()
            .ProducesProblem(400);

        group.MapGet("/export/status/{jobId}", GetExportStatus)
            .WithName("GetExportStatus")
            .WithSummary("Check the status of a queued export job")
            .Produces<ExportJobResponse>()
            .ProducesProblem(404);

        group.MapGet("/export/download/{jobId}", DownloadExport)
            .WithName("DownloadExport")
            .WithSummary("Download a completed export file — frees memory after download")
            .Produces<FileResult>()
            .ProducesProblem(404);

        group.MapPost("/import/users", ImportUsers)
            .WithName("ImportUsers")
            .WithSummary("Upload an Excel file to bulk-import users (max 10MB)")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ImportResult>()
            .ProducesProblem(400)
            .DisableAntiforgery();

        group.MapPost("/import/messages", ImportMessages)
            .WithName("ImportMessages")
            .WithSummary("Upload an Excel file to bulk-import messages (max 10MB)")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ImportResult>()
            .ProducesProblem(400)
            .DisableAntiforgery();

        group.MapGet("/template/users", ExportUserTemplate)
            .WithName("UserImportTemplate")
            .WithSummary("Download a blank Excel template for user imports");

        group.MapGet("/template/messages", ExportMessageTemplate)
            .WithName("MessageImportTemplate")
            .WithSummary("Download a blank Excel template for message imports");
    }


    private static async Task<IResult> ExportUsers(
        ImportExportService service,
        CancellationToken ct
    ) {
        var bytes = await service.ExportUsersAsync(ct);
        return ExcelFileResult(bytes, $"users_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    private static async Task<IResult> ExportMessages(
        ImportExportService service,
        CancellationToken ct
    ) {
        var bytes = await service.ExportMessagesAsync(ct);
        return ExcelFileResult(bytes, $"messages_{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    /// <summary>
    /// Queues an export job for background processing.
    /// Returns 202 Accepted with a jobId the client can poll.
    /// </summary>
    private static IResult QueueExport(
        string type,
        ExportJobStore store,
        IBackgroundJobClient backgroundJobs
    ) {
        var validTypes = new[] { "users", "messages" };
        if (!validTypes.Contains(type.ToLowerInvariant()))
            return Results.BadRequest(new { error = "Invalid type. Use 'users' or 'messages'" });

        var job = new ExportJob { Type = type.ToLowerInvariant() };
        store.Add(job);

        backgroundJobs.Enqueue<HangfireExportJobHandler>(
            handler => handler.ExecuteExport(job.Id, job.Type, CancellationToken.None)
        );

        return Results.Accepted(
            $"/api/admin/import-export/export/status/{job.Id}",
            new ExportJobResponse(job.Id, job.Status, null, null, job.CreatedAt));
    }

    /// <summary>
    /// Returns the current status of a queued export job.
    /// Client should poll this every 1-2 seconds.
    /// </summary>
    private static IResult GetExportStatus(string jobId, ExportJobStore store)
    {
        var job = store.Get(jobId);
        if (job is null)
            return Results.NotFound(new { error = "Job not found or expired" });

        return Results.Ok(new ExportJobResponse(
            job.Id, job.Status, job.FileName, job.Error, job.CreatedAt, job.CompletedAt));
    }

    /// <summary>
    /// Downloads a completed export file.
    /// Calls RemoveResult() after creating the response to free memory.
    /// </summary>
    private static IResult DownloadExport(string jobId, ExportJobStore store)
    {
        var job = store.Get(jobId);
        if (job is null)
            return Results.NotFound(new { error = "Job not found or expired" });

        if (job.Status != ExportJobStatus.Completed)
            return Results.BadRequest(new { error = $"Job is {job.Status}. Wait for completion." });

        var result = store.GetResult(jobId);
        if (result is null || job.FileName is null)
            return Results.StatusCode(500);

        store.RemoveResult(jobId);

        return ExcelFileResult(result, job.FileName);
    }


    private static async Task<IResult> ImportUsers(
        IFormFile file,
        ImportExportService service,
        CancellationToken ct
    ) {
        var validation = ValidateFile(file);
        if (validation is not null) return validation;

        using var stream = file.OpenReadStream();
        var result = await service.ImportUsersAsync(stream, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ImportMessages(
        IFormFile file, ImportExportService service, CancellationToken ct)
    {
        var validation = ValidateFile(file);
        if (validation is not null) return validation;

        using var stream = file.OpenReadStream();
        var result = await service.ImportMessagesAsync(stream, ct);
        return Results.Ok(result);
    }


    private static IResult ExportUserTemplate()
    {
        var headers = new[] { "Username", "Display Name", "Role (user/admin)" };
        var example = new[] { "john_doe", "John Doe", "user" };
        var bytes = ExcelExportHelper.WriteTemplate(headers, "Users Template", example);
        return ExcelFileResult(bytes, "user_import_template.xlsx");
    }

    private static IResult ExportMessageTemplate()
    {
        var headers = new[] { "Channel ID", "User Name", "Text", "Created At (yyyy-MM-dd HH:mm:ss)" };
        var example = new[] { "1", "john_doe", "Hello world!", "2026-01-15 10:30:00" };
        var bytes = ExcelExportHelper.WriteTemplate(headers, "Messages Template", example);
        return ExcelFileResult(bytes, "message_import_template.xlsx");
    }


    /// <summary>
    /// Validates an uploaded file before processing.
    /// Rejects files over 10MB, non-.xlsx files, and empty files.
    /// </summary>
    private static IResult? ValidateFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest(new { error = "No file uploaded" });

        if (file.Length > MaxUploadFileSize)
            return Results.BadRequest(new { error = $"File size exceeds 10MB limit ({file.Length / 1024 / 1024}MB uploaded)" });

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "Only .xlsx files are supported" });

        return null;
    }

    /// <summary>Creates a styled Excel file download response.</summary>
    private static IResult ExcelFileResult(byte[] bytes, string fileName)
    {
        return Results.File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}