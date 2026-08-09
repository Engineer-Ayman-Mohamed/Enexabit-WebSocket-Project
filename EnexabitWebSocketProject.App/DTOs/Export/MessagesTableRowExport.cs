namespace EnexabitWebSocketProject.App.DTOs.Export;

/// <summary>Row in the messages Excel export.</summary>
public record MessagesTableRowExport(
    int Id,
    int ChannelId,
    string ChannelName,
    string UserName,
    string Text,
    DateTime CreatedAt
); 