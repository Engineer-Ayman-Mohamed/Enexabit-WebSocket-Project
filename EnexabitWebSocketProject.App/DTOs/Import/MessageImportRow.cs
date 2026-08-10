namespace EnexabitWebSocketProject.App.DTOs.Import;

/// <summary>Raw row parsed from an uploaded messages Excel file, before validation.</summary>
public sealed class MessageImportRow
{
    public int RowNumber { get; init; }
    public int ChannelId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}