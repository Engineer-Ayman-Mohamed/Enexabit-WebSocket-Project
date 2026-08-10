using EnexabitWebSocketProject.App.Data;
using EnexabitWebSocketProject.App.DTOs.Export;
using EnexabitWebSocketProject.App.DTOs.Import;
using EnexabitWebSocketProject.App.Helpers;
using EnexabitWebSocketProject.App.Models;
using Microsoft.EntityFrameworkCore;

namespace EnexabitWebSocketProject.App.Services;

/// <summary>
/// Orchestrates Excel import/export for Users and Messages.
/// Handles validation, database persistence, transaction safety, and concurrency protection.
/// </summary>
public class ImportExportService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ImportExportService> _logger;

    public ImportExportService(AppDbContext context, ILogger<ImportExportService> logger)
    {
        _context = context;
        _logger = logger;
    }
    

    /// <summary>Exports all users as an Excel file. Password hashes are never included.</summary>
    /// <remarks>Uses AsNoTracking() to avoid change tracking overhead on read-only data.</remarks>
    public async Task<byte[]> ExportUsersAsync(CancellationToken ct)
    {
        var users = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(
                u => new UserTableRowExport(
                u.Id,
                u.Username,
                u.DisplayName,
                u.Role,
                u.CreatedAt
            ))
            .ToListAsync(ct);

        _logger.LogInformation("Exported {Count} users to Excel", users.Count);
        return ExcelExportHelper.WriteUsers(users);
    }

    /// <summary>Exports all messages as an Excel file.</summary>
    public async Task<byte[]> ExportMessagesAsync(CancellationToken ct)
    {
        var messages = await _context.Messages
            .AsNoTracking()
            .Include(m => m.Channel)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MessagesTableRowExport(
                m.Id,
                m.ChannelId,
                m.Channel.Name,
                m.UserName,
                m.Text,
                m.CreatedAt))
            .ToListAsync(ct);

        _logger.LogInformation("Exported {Count} messages to Excel", messages.Count);
        return ExcelExportHelper.WriteMessages(messages);
    }


    /// <summary>
    /// Imports users from an Excel file.
    /// Validates all rows, inserts valid ones in a transaction, returns a summary report.
    /// </summary>
    /// <remarks>
    /// Transaction safety: all valid rows insert atomically. On any failure, full rollback.
    /// Concurrency: pre-checks unique usernames + catches DbUpdateException for race conditions.
    /// </remarks>
    public async Task<ImportResult> ImportUsersAsync(
        Stream fileStream,
        CancellationToken ct
    ) {
        var rows = ExcelExportHelper
            .ReadUsers(fileStream, out var headerErrors);
        
        if (headerErrors.Count > 0)
            return new ImportResult(0, 0, 0, headerErrors);

        if (rows.Count == 0)
            return new ImportResult(0, 0, 0,
                [new ImportRowError(0, "File", "No data rows found. Ensure row 1 is headers and data starts at row 2.")]);

        var errors = ValidateUserRows(rows);
        var validRows = rows
            .Where(r => !errors.Any(e => e.RowNumber == r.RowNumber))
            .ToList();
        
        if (validRows.Count == 0)
            return new ImportResult(rows.Count, 0, rows.Count, errors);

        validRows = CheckDuplicateUsernames(validRows, errors);
        if (validRows.Count == 0)
            return new ImportResult(rows.Count, 0, rows.Count, errors);

        var usernamesToCheck = validRows
            .Select(r => r.Username)
            .Distinct()
            .ToList();

        var existingUsernames = await _context.Users
            .Where(u => usernamesToCheck.Contains(u.Username))
            .Select(u => u.Username)
            .ToListAsync(ct);

        foreach (var row in validRows
                     .Where(r => existingUsernames.Contains(r.Username, StringComparer.OrdinalIgnoreCase)))
        {
            errors.Add(new ImportRowError(row.RowNumber, "Username",
                $"Username '{row.Username}' already exists in the database"));
        }
        validRows = validRows
            .Where(r => !errors.Any(e => e.RowNumber == r.RowNumber))
            .ToList();

        if (validRows.Count == 0)
            return new ImportResult(rows.Count, 0, rows.Count, errors);
        return await InsertUsersAsync(validRows, rows.Count, errors, ct);
    }


    /// <summary>Imports messages from an Excel file.</summary>
    public async Task<ImportResult> ImportMessagesAsync(
        Stream fileStream,
        CancellationToken ct
    ) {
        var rows = ExcelExportHelper
            .ReadMessages(fileStream, out var headerErrors);
        
        if (headerErrors.Count > 0)
            return new ImportResult(0, 0, 0, headerErrors);

        if (rows.Count == 0)
            return new ImportResult(0, 0, 0,
                [new ImportRowError(0, "File", "No data rows found.")]);

        var errors = ValidateMessageRows(rows);
        var validRows = rows
            .Where(r => !errors.Any(e => e.RowNumber == r.RowNumber))
            .ToList();

        if (validRows.Count == 0)
            return new ImportResult(rows.Count, 0, rows.Count, errors);

        var channelIds = validRows
            .Select(r => r.ChannelId)
            .Distinct()
            .ToList();
        
        var existingChannelIds = await _context.Channels
            .Where(c => channelIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);

        foreach (var row in validRows
            .Where(r => !existingChannelIds.Contains(r.ChannelId)))
        {
            errors.Add(new ImportRowError(row.RowNumber, "ChannelId",
                $"Channel ID {row.ChannelId} does not exist"));
        }
        validRows = validRows
            .Where(r => !errors.Any(e => e.RowNumber == r.RowNumber))
            .ToList();

        if (validRows.Count == 0)
            return new ImportResult(rows.Count, 0, rows.Count, errors);
        return await InsertMessagesAsync(validRows, rows.Count, errors, ct);
    }


    /// <summary>
    /// Inserts validated user rows in a single database transaction.
    /// Rolls back entirely on any failure.
    /// </summary>
    private async Task<ImportResult> InsertUsersAsync(
        List<UserImportRow> validRows,
        int totalRows,
        List<ImportRowError> errors,
        CancellationToken ct
    )
    {
        var executionStrategy = _context.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var entities = validRows.Select(r => new User
                {
                    Username = r.Username,
                    DisplayName = r.DisplayName,
                    Role = r.Role.ToLowerInvariant(),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("pass123"),
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                await _context.Users.AddRangeAsync(entities, ct);
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation("Imported {Count} users from Excel", entities.Count);
                return new ImportResult(totalRows, entities.Count, totalRows - entities.Count, errors);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(ct);

                var detail = ex.InnerException?.Message ?? ex.Message;
                _logger.LogWarning("User import failed due to DB constraint: {Detail}", detail);

                errors.Add(new ImportRowError(0, "Database",
                    $"Import failed — possible duplicate username detected by database constraint: {detail}"));
                return new ImportResult(totalRows, 0, totalRows, errors);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "User import transaction failed");
                errors.Add(new ImportRowError(0, "Transaction", $"Import failed: {ex.Message}"));
                return new ImportResult(totalRows, 0, totalRows, errors);
            }
        });
    }

    /// <summary>Inserts validated message rows in a single database transaction.</summary>
    private async Task<ImportResult> InsertMessagesAsync(
        List<MessageImportRow> validRows,
        int totalRows,
        List<ImportRowError> errors,
        CancellationToken ct
    ) {
        var executionStrategy = _context.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var entities = validRows.Select(r => new Message
                {
                    ChannelId = r.ChannelId,
                    UserName = r.UserName,
                    Text = Sanitizer.StripHtml(r.Text),
                    CreatedAt = r.CreatedAt
                }).ToList();

                await _context.Messages.AddRangeAsync(entities, ct);
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation("Imported {Count} messages from Excel", entities.Count);
                return new ImportResult(totalRows, entities.Count, totalRows - entities.Count, errors);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(ct);
                var detail = ex.InnerException?.Message ?? ex.Message;
                errors.Add(new ImportRowError(0, "Database", $"Import failed: {detail}"));
                return new ImportResult(totalRows, 0, totalRows, errors);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Message import transaction failed");
                errors.Add(new ImportRowError(0, "Transaction", $"Import failed: {ex.Message}"));
                return new ImportResult(totalRows, 0, totalRows, errors);
            }
        });
    }


    /// <summary>
    /// Validates all user rows against DB constraints.
    /// Collects ALL errors per row — does not stop at first error.
    /// </summary>
    private static List<ImportRowError> ValidateUserRows(List<UserImportRow> rows)
    {
        var errors = new List<ImportRowError>();
        var validRoles = new[] { "user", "admin" };

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Username))
                errors.Add(new(row.RowNumber, "Username", "Username is required"));
            else if (row.Username.Length > 50)
                errors.Add(new(row.RowNumber, "Username",
                    $"Username exceeds 50 characters (got {row.Username.Length})"));

            if (string.IsNullOrWhiteSpace(row.DisplayName))
                errors.Add(new(row.RowNumber, "DisplayName", "Display name is required"));
            else if (row.DisplayName.Length > 100)
                errors.Add(new(row.RowNumber, "DisplayName",
                    $"Display name exceeds 100 characters (got {row.DisplayName.Length})"));

            if (string.IsNullOrWhiteSpace(row.Role))
                errors.Add(new(row.RowNumber, "Role", "Role is required"));
            else if (!validRoles.Contains(row.Role, StringComparer.OrdinalIgnoreCase))
                errors.Add(new(row.RowNumber, "Role",
                    $"Invalid role '{row.Role}'. Must be one of: {string.Join(", ", validRoles)}"));
        }
        return errors;
    }

    /// <summary>Validates all message rows against DB constraints.</summary>
    private static List<ImportRowError> ValidateMessageRows(List<MessageImportRow> rows)
    {
        var errors = new List<ImportRowError>();

        foreach (var row in rows)
        {
            if (row.ChannelId <= 0)
                errors.Add(new(row.RowNumber, "ChannelId", "Channel ID must be a positive integer"));

            if (string.IsNullOrWhiteSpace(row.UserName))
                errors.Add(new(row.RowNumber, "UserName", "User name is required"));
            else if (row.UserName.Length > 100)
                errors.Add(new(row.RowNumber, "UserName", $"User name exceeds 100 characters"));

            if (string.IsNullOrWhiteSpace(row.Text))
                errors.Add(new(row.RowNumber, "Text", "Message text is required"));
            else if (row.Text.Length > 4000)
                errors.Add(new(row.RowNumber, "Text",
                    $"Message text exceeds 4000 characters (got {row.Text.Length})"));
        }

        return errors;
    }

    /// <summary>
    /// Checks for duplicate usernames within the uploaded file.
    /// Catches duplicates before they hit the database.
    /// </summary>
    private static List<UserImportRow> CheckDuplicateUsernames(
        List<UserImportRow> validRows,
        List<ImportRowError> errors
    ) {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in validRows)
        {
            if (seen.TryGetValue(row.Username, out var firstRow))
            {
                errors.Add(new ImportRowError(row.RowNumber, "Username",
                    $"Duplicate username '{row.Username}' (first seen at row {firstRow})"));
            }
            else
            {
                seen[row.Username] = row.RowNumber;
            }
        }

        return validRows
            .Where(r => !errors.Any(e => e.RowNumber == r.RowNumber))
            .ToList();
    }
}