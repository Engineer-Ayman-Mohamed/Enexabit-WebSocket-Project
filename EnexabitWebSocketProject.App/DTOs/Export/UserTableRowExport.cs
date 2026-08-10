namespace EnexabitWebSocketProject.App.DTOs.Export;

/// <summary>Row in the users Excel export.</summary>
public record UserTableRowExport(
    int Id,
    string Username,
    string DisplayName,
    string Role,
    DateTime CreatedAt
);