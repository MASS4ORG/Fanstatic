using System.Text.RegularExpressions;

namespace Fanstatic.Commands.ValidateLinks;

/// <summary>
/// Provides helper methods for working with HTML content.
/// </summary>
public static partial class HtmlHelper
{
    /// <summary>
    /// Gets a compiled regular expression to match anchor tags with href attributes.
    /// </summary>
    /// <returns>A compiled <see cref="Regex"/> instance for matching anchor tags.</returns>
    [GeneratedRegex("<a[^>]+href=\"([^\"]+)\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();

    /// <summary>
    /// Gets a compiled regular expression to match id or name attributes.
    /// </summary>
    /// <returns>A compiled <see cref="Regex"/> instance for matching id or name attributes.</returns>
    [GeneratedRegex("id=\"([^\"]*)\"|name=\"([^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex IdRegex();

    /// <summary>
    /// Extracts all links from the provided HTML content.
    /// </summary>
    /// <param name="html">The HTML content to extract links from.</param>
    /// <returns>An enumerable collection of link URLs.</returns>
    public static IEnumerable<string> ExtractLinks(string html)
    {
        var matches = LinkRegex().Matches(html);
        return matches.Select(m => m.Groups[1].Value);
    }

    /// <summary>
    /// Determines whether the provided HTML content contains a specific fragment identifier.
    /// </summary>
    /// <param name="html">The HTML content to search.</param>
    /// <param name="fragment">The fragment identifier to search for.</param>
    /// <returns><c>true</c> if the fragment identifier is found; otherwise, <c>false</c>.</returns>
    public static bool HasFragmentId(string html, string fragment)
    {
        var matches = IdRegex().Matches(html);
        return matches.Any(m =>
            (m.Groups[1].Success && m.Groups[1].Value == fragment) ||
            (m.Groups[2].Success && m.Groups[2].Value == fragment));
    }
}
