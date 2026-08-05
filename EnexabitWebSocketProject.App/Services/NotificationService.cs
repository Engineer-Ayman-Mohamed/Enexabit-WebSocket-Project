using System.Text.RegularExpressions;
using EnexabitWebSocketProject.App.Data;

namespace EnexabitWebSocketProject.App.Services;

public class NotificationService
{
    private readonly AppDbContext _context;
    
    /// <summary>Regex pattern for matching @mentions in messages.</summary>
    private static readonly Regex _mentionRegex = new(
        @"@(\w+)",
        RegexOptions.Compiled | RegexOptions.NonBacktracking
    );
    
    public NotificationService(AppDbContext context)
    {
        _context = context;
    }
}