using System.Net;
using System.Text;
using Fanstatic.Models;
using QRCoder;

namespace Fanstatic.Shortcodes;

/// <summary>
/// The <c>qr</c> built-in: a QR code of <c>text</c> (or the inner content) as an inline PNG <c>&lt;img&gt;</c>.
/// Arguments: <c>text</c>, <c>level</c> (L, M, Q, H), <c>scale</c>, <c>alt</c>, <c>class</c>, <c>title</c>, <c>loading</c>.
/// </summary>
public sealed class QrShortcode : IBuiltinShortcode
{
    const int DefaultScale = 4;

    /// <inheritdoc/>
    public string Name => "qr";

    /// <inheritdoc/>
    public bool UsesInner => true;

    /// <inheritdoc/>
    public IReadOnlyList<string> Positional => [];

    /// <inheritdoc/>
    public string Render(ShortcodeContext shortcode, ISite site, IPage page)
    {
        ArgumentNullException.ThrowIfNull(shortcode);

        var text = shortcode.GetString(0, "text") ?? shortcode.Inner?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("requires 'text' or inner content");
        }

        var scale = shortcode.Get("scale") is { } scaleValue
            ? (int)ShortcodeBinder.Coerce("scale", "int", scaleValue)
            : DefaultScale;
        if (scale < 1)
        {
            throw new ArgumentException("'scale' must be at least 1");
        }

        using var data = QRCodeGenerator.GenerateQrCode(text, ParseLevel(shortcode.GetString(-1, "level")));
        var png = new PngByteQRCode(data).GetGraphic(scale);
        var size = data.ModuleMatrix.Count * scale;

        var html = new StringBuilder("<img src=\"data:image/png;base64,")
            .Append(Convert.ToBase64String(png))
            .Append("\" width=\"").Append(size).Append("\" height=\"").Append(size).Append('"');
        AppendAttribute(html, "alt", shortcode.GetString(-1, "alt"));
        AppendAttribute(html, "class", shortcode.GetString(-1, "class"));
        AppendAttribute(html, "title", shortcode.GetString(-1, "title"));
        AppendAttribute(html, "loading", shortcode.GetString(-1, "loading"));
        return html.Append('>').ToString();
    }

    static readonly Dictionary<string, QRCodeGenerator.ECCLevel> Levels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["L"] = QRCodeGenerator.ECCLevel.L,
        ["M"] = QRCodeGenerator.ECCLevel.M,
        ["Q"] = QRCodeGenerator.ECCLevel.Q,
        ["H"] = QRCodeGenerator.ECCLevel.H
    };

    static QRCodeGenerator.ECCLevel ParseLevel(string? level)
    {
        if (string.IsNullOrEmpty(level))
        {
            return QRCodeGenerator.ECCLevel.M;
        }

        return Levels.TryGetValue(level, out var parsed)
            ? parsed
            : throw new ArgumentException($"'level' must be one of L, M, Q, H; got '{level}'");
    }

    static void AppendAttribute(StringBuilder html, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            html.Append(' ').Append(name).Append("=\"").Append(WebUtility.HtmlEncode(value)).Append('"');
        }
    }
}
