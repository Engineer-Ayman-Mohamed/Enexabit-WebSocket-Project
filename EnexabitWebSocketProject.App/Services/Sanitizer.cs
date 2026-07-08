using System.Text.RegularExpressions;

namespace EnexabitWebSocketProject.App.Services;

public static partial class Sanitizer
{
    [GeneratedRegex(@"<[^>]*>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    public static string StripHtml(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        return HtmlTagRegex().Replace(input, "").Trim();
    }
}