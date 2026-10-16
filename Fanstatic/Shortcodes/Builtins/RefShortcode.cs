using Fanstatic.Models;
using Fanstatic.Parsers;

namespace Fanstatic.Shortcodes;

/// <summary>
/// The <c>ref</c> (absolute) and <c>relref</c> (relative) built-ins: the permalink of another content file.
/// Arguments: <c>path</c> (or the first positional), <c>lang</c> and <c>outputFormat</c>.
/// </summary>
public sealed class RefShortcode(bool isAbsolute) : IBuiltinShortcode
{
    /// <inheritdoc/>
    public string Name => isAbsolute ? "ref" : "relref";

    /// <inheritdoc/>
    public bool UsesInner => false;

    /// <inheritdoc/>
    public IReadOnlyList<string> Positional => ["path"];

    /// <inheritdoc/>
    public string Render(ShortcodeContext shortcode, ISite site, IPage page)
    {
        ArgumentNullException.ThrowIfNull(shortcode);

        return RefShortcodeParser.Resolve(site, page,
            shortcode.GetString(0, "path"),
            shortcode.GetString(1, "lang"),
            shortcode.GetString(2, "outputFormat") ?? shortcode.GetString(2, "outputformat"),
            isAbsolute);
    }
}
