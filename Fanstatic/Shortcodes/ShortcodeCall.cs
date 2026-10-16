namespace Fanstatic.Shortcodes;

/// <summary>
/// One shortcode invocation in the content, with its nested calls.
/// </summary>
public sealed class ShortcodeCall
{
    /// <summary>
    /// The shortcode name, empty for an escaped tag.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The delimiter style of the opening tag.
    /// </summary>
    public ShortcodeNotation Notation { get; init; }

    /// <summary>
    /// The arguments in source order.
    /// </summary>
    public IReadOnlyList<ShortcodeArgument> Arguments { get; init; } = [];

    /// <summary>
    /// The enclosing call, null at the top level.
    /// </summary>
    public ShortcodeCall? Parent { get; init; }

    /// <summary>
    /// Zero-based position among the calls of the same parent.
    /// </summary>
    public int Ordinal { get; init; }

    /// <summary>
    /// Offset of the opening tag.
    /// </summary>
    public int Start { get; init; }

    /// <summary>
    /// Offset just past the closing tag, or past the opening tag for a standalone call.
    /// </summary>
    public int End { get; set; }

    /// <summary>
    /// Offset of the inner content, -1 for a standalone call.
    /// </summary>
    public int InnerStart { get; init; } = -1;

    /// <summary>
    /// Offset just past the inner content, -1 for a standalone call.
    /// </summary>
    public int InnerEnd { get; set; } = -1;

    /// <summary>
    /// Calls nested in the inner content, in order.
    /// </summary>
    public List<ShortcodeCall> Children { get; } = [];

    /// <summary>
    /// The literal text of an escaped tag, null for a real call.
    /// </summary>
    public string? Literal { get; init; }

    /// <summary>
    /// True when the call has a closing tag and therefore inner content.
    /// </summary>
    public bool IsPaired => InnerStart >= 0;
}
