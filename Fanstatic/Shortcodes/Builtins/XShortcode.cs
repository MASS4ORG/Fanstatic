using System.Net;
using System.Text.Json;
using Fanstatic.Commands.ValidateLinks;
using Fanstatic.Models;

namespace Fanstatic.Shortcodes;

/// <summary>
/// The <c>x</c> built-in: an X (Twitter) post embedded through the <c>publish.x.com</c> oEmbed endpoint.
/// Responses are cached on disk; when the endpoint is unreachable a plain link to the post is rendered and a
/// warning is logged. Usage: <c>{{&lt; x user="SanDiegoZoo" id="1453110110599868418" &gt;}}</c>.
/// </summary>
public sealed class XShortcode(IHttpClientWrapper http, string cacheDirectory) : IBuiltinShortcode
{
    /// <summary>
    /// Default cache folder for oEmbed responses, under the system temporary folder.
    /// </summary>
    public static string DefaultCacheDirectory => Path.Combine(Path.GetTempPath(), "fanstatic-cache", "x");

    /// <inheritdoc/>
    public string Name => "x";

    /// <inheritdoc/>
    public bool UsesInner => false;

    /// <inheritdoc/>
    public IReadOnlyList<string> Positional => [];

    /// <inheritdoc/>
    public string Render(ShortcodeContext shortcode, ISite site, IPage page)
    {
        ArgumentNullException.ThrowIfNull(shortcode);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(page);

        var user = shortcode.GetString(-1, "user");
        var id = shortcode.GetString(-1, "id");
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("requires two named parameters: user and id");
        }

        var url = $"https://x.com/{Uri.EscapeDataString(user)}/status/{Uri.EscapeDataString(id)}";
        var cacheFile = Path.Combine(cacheDirectory, $"{SafeFileName(user)}-{SafeFileName(id)}.html");
        if (File.Exists(cacheFile))
        {
            return File.ReadAllText(cacheFile);
        }

        try
        {
            var html = Fetch(url);
            Directory.CreateDirectory(cacheDirectory);
            File.WriteAllText(cacheFile, html);
            return html;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException
                                       or KeyNotFoundException or TaskCanceledException)
        {
            site.Logger.Warning("The x shortcode could not retrieve {Url} for {File}: {Reason}", url,
                page.SourceRelativePath, ex.Message);
            var encoded = WebUtility.HtmlEncode(url);
            return $"<blockquote class=\"twitter-tweet\"><a href=\"{encoded}\">{encoded}</a></blockquote>";
        }
    }

    string Fetch(string url)
    {
        var request = new Uri($"https://publish.x.com/oembed?url={Uri.EscapeDataString(url)}&dnt=true");
        using var response = http.GetAsync(request).GetAwaiter().GetResult();
        _ = response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        return json.RootElement.GetProperty("html").GetString()
               ?? throw new InvalidOperationException("the oEmbed response has no html");
    }

    static string SafeFileName(string value) =>
        new([.. value.Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_')]);
}
