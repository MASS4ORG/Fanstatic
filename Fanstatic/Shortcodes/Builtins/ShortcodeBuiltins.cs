using Fanstatic.Commands.ValidateLinks;

namespace Fanstatic.Shortcodes;

/// <summary>
/// The C# built-in shortcodes. Liquid built-ins are embedded resources under <c>wwwroot/shortcodes/</c>.
/// </summary>
public static class ShortcodeBuiltins
{
    static readonly IHttpClientWrapper Http = new HttpClientWrapper();

    /// <summary>
    /// Creates one instance of each C# built-in.
    /// </summary>
    public static IReadOnlyList<IBuiltinShortcode> Create() =>
    [
        new RefShortcode(isAbsolute: true),
        new RefShortcode(isAbsolute: false),
        new ParamShortcode(),
        new HighlightShortcode(),
        new QrShortcode(),
        new XShortcode(Http, XShortcode.DefaultCacheDirectory)
    ];
}
