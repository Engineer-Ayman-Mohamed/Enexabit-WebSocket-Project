namespace EnexabitWebSocketProject.App.DTOs.Import;

/// <summary>A single validation error for a specific row/column during import.</summary>
public record ImportRowError(
    int RowNumber,
    string Column,
    string Message
);