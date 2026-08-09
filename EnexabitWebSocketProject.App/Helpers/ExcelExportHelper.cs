using System.Globalization;
using ClosedXML.Excel;
using EnexabitWebSocketProject.App.DTOs.Export;
using EnexabitWebSocketProject.App.DTOs.Import;
using StackExchange.Redis;

namespace EnexabitWebSocketProject.App.Helpers;

public class ExcelExportHelper
{
    private const string IsoDateTimeFormat = "yyyy-MM-dd HH:mm:ss";
    
    /// <summary>Writes user data to an Excel byte array with styled headers.</summary>
    /// <param name="users">Users to export. Passwords are never included.</param>
    public static byte[] WriteUsers(IReadOnlyList<UserTableRowExport> users)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Users");

        var headers = new[] { "ID", "Username", "Display Name", "Role", "Created At" };
        WriteHeaders(sheet, headers);

        for (int i = 0; i < users.Count; i++)
        {
            var row = i + 2;
            sheet.Cell(row, 1).Value = users[i].Id;
            sheet.Cell(row, 2).Value = users[i].Username;
            sheet.Cell(row, 3).Value = users[i].DisplayName;
            sheet.Cell(row, 4).Value = users[i].Role;
            sheet.Cell(row, 5).Value = users[i].CreatedAt
                .ToString(IsoDateTimeFormat, CultureInfo.InvariantCulture);
        }

        AutoFitColumns(sheet, headers.Length);
        return ToByteArray(workbook);
    }

    /// <summary>Writes message data to an Excel byte array with styled headers.</summary>
    public static byte[] WriteMessages(IReadOnlyList<MessagesTableRowExport> messages)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Messages");

        var headers = new[] { "ID", "Channel ID", "Channel Name", "User", "Text", "Created At" };
        WriteHeaders(sheet, headers);

        for (int i = 0; i < messages.Count; i++)
        {
            var row = i + 2;
            sheet.Cell(row, 1).Value = messages[i].Id;
            sheet.Cell(row, 2).Value = messages[i].ChannelId;
            sheet.Cell(row, 3).Value = messages[i].ChannelName;
            sheet.Cell(row, 4).Value = messages[i].UserName;
            sheet.Cell(row, 5).Value = messages[i].Text;
            sheet.Cell(row, 6).Value = messages[i].CreatedAt
                .ToString(IsoDateTimeFormat, CultureInfo.InvariantCulture);
        }

        AutoFitColumns(sheet, headers.Length);
        return ToByteArray(workbook);
    }

    /// <summary>Generates a blank template Excel file with headers and an example row.</summary>
    public static byte[] WriteTemplate(
        string[] headers,
        string sheetName,
        string[]? exampleRow = null
    ) {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        WriteHeaders(sheet, headers);

        if (exampleRow is not null)
        {
            for (int i = 0; i < exampleRow.Length && i < headers.Length; i++)
                sheet.Cell(2, i + 1).Value = exampleRow[i];
        }

        AutoFitColumns(sheet, headers.Length);
        return ToByteArray(workbook);
    }

    /// <summary>
    /// Parses an uploaded Excel stream into user import rows.
    /// Validates that the header row matches expected columns before parsing data.
    /// </summary>
    /// <param name="fileStream">The uploaded .xlsx file stream.</param>
    /// <param name="errors">Populated with header validation errors if any.</param>
    public static List<UserImportRow> ReadUsers(
        Stream fileStream,
        out List<ImportRowError> errors
    ) {
        errors = [];
        using var workbook = new XLWorkbook(fileStream);
        var sheet = workbook.Worksheets.First();
        var rows = new List<UserImportRow>();

        var expectedHeaders = new[] { "username", "display name", "role" };
        if (!ValidateHeaders(sheet, expectedHeaders, errors))
            return rows;

        var dataRange = sheet.RangeUsed();
        if (dataRange is null || dataRange.RowCount() < 2)
            return rows;

        for (int row = 2; row <= dataRange.RowCount(); row++)
        {
            rows.Add(
            new UserImportRow {
                RowNumber = row,
                Username = GetCellText(sheet, row, 1),
                DisplayName = GetCellText(sheet, row, 2),
                Role = GetCellText(sheet, row, 3) is { Length: > 0 } r ? r : "user"
            });
        }
        return rows;
    }

    /// <summary>Parses an uploaded Excel stream into message import rows.
    /// Validates headers before parsing. Enforces UTC dates.</summary>
    public static List<MessageImportRow> ReadMessages(
        Stream fileStream,
        out List<ImportRowError> errors
    ) {
        errors = [];
        using var workbook = new XLWorkbook(fileStream);
        var sheet = workbook.Worksheets.First();
        var rows = new List<MessageImportRow>();

        var expectedHeaders = new[] { "channel id", "user name", "text", "created at" };
        if (!ValidateHeaders(sheet, expectedHeaders, errors))
            return rows;

        var dataRange = sheet.RangeUsed();
        if (dataRange is null || dataRange.RowCount() < 2)
            return rows;

        for (int row = 2; row <= dataRange.RowCount(); row++)
        {
            var dateText = GetCellText(sheet, row, 4);
            var createdAt = ParseUtcDateTime(dateText);

            rows.Add(new MessageImportRow
            {
                RowNumber = row,
                ChannelId = int.TryParse(GetCellText(sheet, row, 1), out var cid) ? cid : 0,
                UserName = GetCellText(sheet, row, 2),
                Text = GetCellText(sheet, row, 3),
                CreatedAt = createdAt
            });
        }

        return rows;
    }

    /// <summary>
    /// Validates that the header row contains all expected columns (case-insensitive).
    /// Adds errors for missing columns.
    /// </summary>
    private static bool ValidateHeaders(IXLWorksheet sheet, string[] expectedHeaders, List<ImportRowError> errors)
    {
        var headerRange = sheet.Range(1, 1, 1, sheet.ColumnsUsed().Count());
        var actualHeaders = new List<string>();
        foreach (var cell in headerRange.Row(1).Cells())
            actualHeaders.Add(cell.GetString().Trim().ToLowerInvariant());

        var missing = expectedHeaders
            .Where(h => !actualHeaders.Contains(h))
            .ToList();

        if (missing.Count > 0)
        {
            errors.Add(new ImportRowError(
                1, "Headers",
                $"Missing required columns: {string.Join(", ", missing)}. " +
                $"Expected order: {string.Join(", ", expectedHeaders)}"));
            return false;
        }

        return true;
    }

    /// <summary>Reads a cell value as trimmed string, returns empty string for null/blank.</summary>
    private static string GetCellText(IXLWorksheet sheet, int row, int col)
    {
        var val = sheet.Cell(row, col).GetString();
        return val?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Parses a date string in ISO 8601 format, always returns UTC.
    /// Falls back to DateTime.UtcNow if parsing fails.
    /// </summary>
    private static DateTime ParseUtcDateTime(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return DateTime.UtcNow;

        if (
            DateTime.TryParseExact(text, IsoDateTimeFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var result))
        {
            return result;
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var fallback))
        {
            return fallback;
        }
        return DateTime.UtcNow;
    }

    /// <summary>Applies styled formatting to the header row.</summary>
    private static void WriteHeaders(IXLWorksheet sheet, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];

        var headerRow = sheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromArgb(79, 129, 189);
        headerRow.Style.Font.FontColor = XLColor.White;
        headerRow.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    /// <summary>Auto-fits column widths.</summary>
    private static void AutoFitColumns(IXLWorksheet sheet, int columnCount)
    {
        for (int i = 1; i <= columnCount; i++)
            sheet.Column(i).AdjustToContents();
    }

    /// <summary>Converts workbook to byte array for HTTP response.</summary>
    private static byte[] ToByteArray(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}