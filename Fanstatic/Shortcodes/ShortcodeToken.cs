namespace Fanstatic.Shortcodes;

/// <summary>
/// How a shortcode call is delimited, which decides when its output is spliced into the content.
/// </summary>
public enum ShortcodeNotation
{
    /// <summary>
    /// <c>{{&lt; name &gt;}}</c>: the output is HTML and bypasses Markdown.
    /// </summary>
    Standard,

    /// <summary>
    /// <c>{{% name %}}</c>: the output is spliced into the Markdown before Markdig runs.
    /// </summary>
    Markdown
}

/// <summary>
/// The role of a lexed shortcode tag.
/// </summary>
public enum ShortcodeTokenKind
{
    /// <summary>
    /// An opening or self-closing tag.
    /// </summary>
    Open,

    /// <summary>
    /// A closing tag such as <c>{{&lt; /name &gt;}}</c>.
    /// </summary>
    Close,

    /// <summary>
    /// A commented tag such as <c>{{&lt;/* name */&gt;}}</c>, output literally without the comment markers.
    /// </summary>
    Escaped
}

/// <summary>
/// One shortcode argument: named when <paramref name="Name"/> is set, positional otherwise.
/// </summary>
/// <param name="Name">The argument name, or null for a positional argument.</param>
/// <param name="Value">The typed value: string, bool, int, long, double, list or map.</param>
public sealed record ShortcodeArgument(string? Name, object Value);

/// <summary>
/// One shortcode tag found in the content, located by its character offsets.
/// </summary>
/// <param name="Kind">Whether the tag opens, closes or escapes a shortcode.</param>
/// <param name="Name">The shortcode name, possibly with subdirectories such as <c>media/audio</c>.</param>
/// <param name="Notation">The delimiter style.</param>
/// <param name="Arguments">The arguments in source order.</param>
/// <param name="SelfClosing">True for <c>{{&lt; name /&gt;}}</c>.</param>
/// <param name="Start">Offset of the opening <c>{{</c>.</param>
/// <param name="End">Offset just past the closing <c>}}</c>.</param>
/// <param name="Literal">The text an escaped tag renders to, null otherwise.</param>
public sealed record ShortcodeToken(
    ShortcodeTokenKind Kind,
    string Name,
    ShortcodeNotation Notation,
    IReadOnlyList<ShortcodeArgument> Arguments,
    bool SelfClosing,
    int Start,
    int End,
    string? Literal = null);
