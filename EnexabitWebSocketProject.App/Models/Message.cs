namespace EnexabitWebSocketProject.App.Models;

public class Message
{
    public int Id { get; set; }
    
    public int ChannelId { get; set; }
    public Channel Channel { get; set; } = null!;
    
    public string UserName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

}