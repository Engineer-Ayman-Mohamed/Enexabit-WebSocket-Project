namespace EnexabitWebSocketProject.App.DTOs.Import;

/// <summary>Raw row parsed from an uploaded users Excel file, before validation.</summary>
public sealed class UserImportRow
{
    public int RowNumber { get; init; }
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = "user";
}