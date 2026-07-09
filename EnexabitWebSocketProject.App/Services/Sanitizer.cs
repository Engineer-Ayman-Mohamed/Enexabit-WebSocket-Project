using System.Text.RegularExpressions;

namespace EnexabitWebSocketProject.App.Services;

/// <summary>Provides XSS prevention by stripping HTML tags from user input.</summary>
public static partial class Sanitizer
{
    /// <summary>Compiled regex matching any HTML tag: <c>&lt;...&gt;</c>.</summary>
    [GeneratedRegex(@"<[^>]*>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex(); 

    /// <summary>Removes all HTML tags from the input string and trims whitespace.</summary>
    /// <param name="input">Raw user input that may contain HTML.</param>
    /// <returns>Input with all HTML tags removed, or <c>null</c>/empty if the input was null/empty.</returns>
    /// <example>
    /// Sanitizer.StripHtml("Hello &lt;script&gt;alert('xss')&lt;/script&gt;") returns "Hello"
    /// </example>
    public static string StripHtml(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        return HtmlTagRegex().Replace(input, "").Trim();
    }
}