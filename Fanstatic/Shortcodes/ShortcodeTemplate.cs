using Fanstatic.Models;

namespace Fanstatic.Shortcodes;

/// <summary>
/// A resolved shortcode: a Liquid template file, an embedded Liquid body or a C# built-in.
/// </summary>
/// <param name="Name">The shortcode name.</param>
/// <param name="Path">Full path of the template file, null for embedded and C# built-ins.</param>
/// <param name="Body">Liquid body of an embedded built-in, null otherwise.</param>
/// <param name="Builtin">The C# implementation, null for Liquid templates.</param>
/// <param name="UsesInner">True when the template reads <c>inner</c>, so calls must be closed.</param>
/// <param name="Schema">The parameter schema, null when the template declares none.</param>
public sealed record ShortcodeTemplate(
    string Name,
    string? Path,
    string? Body,
    IBuiltinShortcode? Builtin,
    bool UsesInner,
    ShortcodeSchema? Schema)
{
    /// <summary>
    /// Names that positional arguments map to, from the schema or the built-in.
    /// </summary>
    public IReadOnlyList<string> PositionalNames => Schema?.Positional ?? Builtin?.Positional ?? [];
}

/// <summary>
/// A shortcode implemented in C#, overridable by a site or theme template of the same name.
/// </summary>
public interface IBuiltinShortcode
{
    /// <summary>
    /// The shortcode name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// True when calls take inner content and must be closed.
    /// </summary>
    bool UsesInner { get; }

    /// <summary>
    /// Names that positional arguments map to; declaring them allows mixing positional and named arguments.
    /// </summary>
    IReadOnlyList<string> Positional { get; }

    /// <summary>
    /// Renders the call to HTML.
    /// </summary>
    string Render(ShortcodeContext shortcode, ISite site, IPage page);
}
