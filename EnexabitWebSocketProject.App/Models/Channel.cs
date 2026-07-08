namespace EnexabitWebSocketProject.App.Models;

public class Channel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public List<Message> Messages { get; set; } = [];
}