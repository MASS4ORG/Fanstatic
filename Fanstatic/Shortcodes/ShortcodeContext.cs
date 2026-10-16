namespace Fanstatic.Shortcodes;

/// <summary>
/// The <c>shortcode</c> variable of a shortcode template: its name, arguments, inner content and location.
/// </summary>
public sealed class ShortcodeContext
{
    /// <summary>
    /// The shortcode name as called.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The arguments: a map for named arguments, a list for positional ones.
    /// </summary>
    public object Params => IsNamedParams ? NamedParams : PositionalParams;

    /// <summary>
    /// True when the arguments are named, so <see cref="Params"/> is a map.
    /// </summary>
    public bool IsNamedParams => NamedParams.Count > 0;

    /// <summary>
    /// The named arguments, after the schema maps positional arguments and applies defaults.
    /// </summary>
    public Dictionary<string, object> NamedParams { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// The positional arguments that no schema name maps.
    /// </summary>
    public List<object> PositionalParams { get; init; } = [];

    /// <summary>
    /// The content between the opening and closing tags with nested shortcodes rendered, null for a standalone call.
    /// </summary>
    public string? Inner { get; set; }

    /// <summary>
    /// <see cref="Inner"/> without the indentation shared by all its lines.
    /// </summary>
    public string? InnerDeindent => Inner is null ? null : Deindent(Inner);

    /// <summary>
    /// Zero-based position among the shortcodes of the same parent.
    /// </summary>
    public int Ordinal { get; init; }

    /// <summary>
    /// The enclosing shortcode, null at the top level.
    /// </summary>
    public ShortcodeContext? Parent { get; init; }

    /// <summary>
    /// Where the call is, as <c>file:line:col</c>; empty for calls from templates.
    /// </summary>
    public string Position => PositionFactory?.Invoke() ?? string.Empty;

    /// <summary>
    /// Computes <see cref="Position"/> on demand, since locating a call reads the content file.
    /// </summary>
    public Func<string>? PositionFactory { get; init; }

    /// <summary>
    /// Returns the argument at a position or with a name, or null when absent.
    /// </summary>
    public object? Get(object key) => key switch
    {
        int index when !IsNamedParams => index >= 0 && index < PositionalParams.Count ? PositionalParams[index] : null,
        string name => NamedParams.GetValueOrDefault(name),
        _ => null
    };

    /// <summary>
    /// Returns the argument at a position or with a name as text, or null when absent.
    /// </summary>
    public string? GetString(int index, string name) =>
        (IsNamedParams ? Get(name) : Get(index)) is { } value ? ShortcodeBinder.ToText(value) : null;

    static string Deindent(string text)
    {
        var lines = text.Split('\n');
        var indent = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Length - line.TrimStart(' ', '\t').Length)
            .DefaultIfEmpty(0)
            .Min();
        return indent == 0
            ? text
            : string.Join('\n', lines.Select(line => line.Length >= indent ? line[indent..] : line.TrimStart(' ', '\t')));
    }
}
