namespace EnexabitWebSocketProject.App.DTOs.Import;

/// <summary>Summary returned after an Excel import operation.</summary>
public record ImportResult(
    int TotalRows,
    int ImportedRows,
    int SkippedRows,
    List<ImportRowError> Errors
);