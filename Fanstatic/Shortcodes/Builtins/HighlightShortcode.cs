using System.Globalization;
using System.Net;
using System.Text;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Markdig;

namespace Fanstatic.Shortcodes;

/// <summary>
/// The <c>highlight</c> built-in: the inner code as a Markdig fenced code block, with the Hugo options mapped to
/// attributes that client-side highlighters read (Prism's line-numbers and line-highlight plugins, highlight.js).
/// Usage: <c>{{&lt; highlight go "linenos=table,hl_lines=2 4-5,linenostart=10" &gt;}}code{{&lt; /highlight &gt;}}</c>.
/// </summary>
public sealed class HighlightShortcode : IBuiltinShortcode
{
    /// <inheritdoc/>
    public string Name => "highlight";

    /// <inheritdoc/>
    public bool UsesInner => true;

    /// <inheritdoc/>
    public IReadOnlyList<string> Positional => ["lang", "options"];

    /// <inheritdoc/>
    public string Render(ShortcodeContext shortcode, ISite site, IPage page)
    {
        ArgumentNullException.ThrowIfNull(shortcode);

        var language = shortcode.GetString(0, "lang") ?? string.Empty;
        var code = (shortcode.Inner ?? string.Empty).Trim('\r', '\n').TrimEnd();
        var fence = new string('`', Math.Max(3, LongestBacktickRun(code) + 1));
        var html = Markdown.ToHtml($"{fence}{language}\n{code}\n{fence}\n", SiteHelper.MarkdownPipeline).TrimEnd();

        var attributes = Attributes(language, ParseOptions(shortcode.GetString(1, "options")));
        return "<div class=\"highlight\">" + html.Replace("<pre>", $"<pre{attributes}>", StringComparison.Ordinal)
                                           + "</div>";
    }

    /// <summary>
    /// Parses Hugo's option string, such as <c>linenos=table,hl_lines=2 4-5</c>, into a map.
    /// </summary>
    public static Dictionary<string, string> ParseOptions(string? options)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (var option in (options ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = option.IndexOf('=');
            var key = (separator < 0 ? option : option[..separator]).Trim();
            if (key.Length > 0)
            {
                result[key] = separator < 0 ? "true" : option[(separator + 1)..].Trim().Trim('"');
            }
        }

        return result;
    }

    static string Attributes(string language, Dictionary<string, string> options)
    {
        var html = new StringBuilder();
        var lineNumbers = options.GetValueOrDefault("linenos");
        AppendClasses(html, language, lineNumbers);
        AppendData(html, "linenos", lineNumbers is "table" or "inline" ? lineNumbers : null);
        AppendLineData(html, options);
        AppendData(html, "line-anchors", options.GetValueOrDefault("lineanchors"));
        AppendData(html, "anchor-linenos", options.GetValueOrDefault("anchorlinenos"));
        return html.ToString();
    }

    static void AppendClasses(StringBuilder html, string language, string? lineNumbers)
    {
        List<string> classes = [];
        if (language.Length > 0)
        {
            classes.Add("language-" + language);
        }

        if (lineNumbers is not null and not "false")
        {
            classes.Add("line-numbers");
        }

        if (classes.Count > 0)
        {
            html.Append(" class=\"").AppendJoin(' ', classes).Append('"');
        }
    }

    static void AppendLineData(StringBuilder html, Dictionary<string, string> options)
    {
        var start = int.TryParse(options.GetValueOrDefault("linenostart"), out var parsedStart) ? parsedStart : 1;
        var highlighted = options.GetValueOrDefault("hl_lines");
        AppendData(html, "start", start != 1 ? start.ToString(CultureInfo.InvariantCulture) : null);
        AppendData(html, "line", highlighted?.Replace(' ', ','));
        AppendData(html, "line-offset",
            start != 1 && highlighted is not null ? (start - 1).ToString(CultureInfo.InvariantCulture) : null);
    }

    static void AppendData(StringBuilder html, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            html.Append(" data-").Append(name).Append("=\"").Append(WebUtility.HtmlEncode(value)).Append('"');
        }
    }

    static int LongestBacktickRun(string code)
    {
        int longest = 0, current = 0;
        foreach (var character in code)
        {
            current = character == '`' ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }

        return longest;
    }
}
