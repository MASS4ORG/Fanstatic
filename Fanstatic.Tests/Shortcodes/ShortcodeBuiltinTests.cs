using System.Net;
using Fanstatic.Commands.ValidateLinks;
using Fanstatic.Models;
using Fanstatic.Shortcodes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Fanstatic.Test.Shortcodes;

/// <summary>
/// Golden-output tests for the built-in shortcodes.
/// </summary>
public class ShortcodeBuiltinTests : TestSetup
{
    string Render(string markdown, params (string Path, string Content)[] files)
    {
        var all = files.ToDictionary(file => file.Path, file => file.Content);
        all["content/a.md"] = "---\ntitle: A\nparams:\n  author:\n    Name: Page Author\n  list: [x, y]\n---\n" + markdown;
        all.TryAdd("fanstatic.yaml",
            "Title: test\nTheme: test\nbaseURL: https://example.com/\nparams:\n  site_only: from site\n");
        using var site = new ShortcodeTestSite(LoggerMock, all);
        return site.Page("a.md").ContentPreRendered.TrimEnd();
    }

    /// <summary>Confirms the details output with a summary, attributes and Markdown inner content.</summary>
    [Fact]
    public void DetailsShouldRenderDisclosure() =>
        Assert.Equal(
            "<details class=\"c\" name=\"n\" open title=\"t\">\n  <summary><em>Sum</em></summary>\n  <p>Hidden <strong>text</strong></p>\n</details>",
            Render("{{< details summary=\"*Sum*\" class=\"c\" name=\"n\" open=true title=\"t\" >}}\nHidden **text**\n{{< /details >}}"));

    /// <summary>Confirms the details defaults.</summary>
    [Fact]
    public void DetailsShouldDefaultSummary() =>
        Assert.Equal("<details>\n  <summary>Details</summary>\n  <p>x</p>\n</details>",
            Render("{{< details >}}x{{< /details >}}"));

    /// <summary>Confirms the figure output with link, caption, attribution and image attributes.</summary>
    [Fact]
    public void FigureShouldRenderImageWithCaption() =>
        Assert.Equal(
            "<figure class=\"f\"><a href=\"/big.jpg\" target=\"_blank\" rel=\"noopener\"><img src=\"/img.jpg\" alt=\"A cat\" width=\"10\" height=\"20\" loading=\"lazy\"></a><figcaption><h4>Title</h4><p>A <em>cat</em>\n          <a href=\"/src\">Photo</a></p>\n    </figcaption>\n</figure>",
            Render("{{< figure src=\"/img.jpg\" link=\"/big.jpg\" target=\"_blank\" rel=\"noopener\" class=\"f\" width=10 height=20 loading=\"lazy\" title=\"Title\" caption=\"A *cat*\" attr=\"Photo\" attrlink=\"/src\" >}}"));

    /// <summary>Confirms that figure resolves a page resource by file name and falls back to the caption for alt text.</summary>
    [Fact]
    public void FigureShouldPreferPageResource()
    {
        using var site = new ShortcodeTestSite(LoggerMock, new Dictionary<string, string>
        {
            ["content/b/index.md"] = "---\ntitle: B\n---\n{{< figure src=\"photo.webp\" caption=\"**Bold** cap\" >}}\n",
            ["content/b/photo.webp"] = "img"
        });

        var html = site.Page("b/index.md").ContentPreRendered;

        Assert.Contains("alt=\"Bold cap\"", html, StringComparison.Ordinal);
        Assert.Contains("<img src=\"/b/photo.webp\"", html, StringComparison.Ordinal);
    }

    /// <summary>Confirms the param output for page, nested, site and list values.</summary>
    [Fact]
    public void ParamShouldReadPageThenSiteParams() =>
        Assert.Equal("<p>Page Author|from site|x, y|Page Author</p>",
            Render("{{< param \"author.name\" >}}|{{< param site_only >}}|{{< param list >}}|{{< param name=\"AUTHOR.NAME\" >}}"));

    /// <summary>Confirms that a missing param fails the build.</summary>
    [Fact]
    public void ParamShouldFailWhenMissing()
    {
        var exception = Assert.Throws<ShortcodeException>(() => Render("{{< param \"nope.deep\" >}}"));

        Assert.Equal("shortcode 'param': param 'nope.deep' not found in the page or site params", exception.Message);
        Assert.Equal("shortcode 'param': requires a parameter name",
            Assert.Throws<ShortcodeException>(() => Render("{{< param \" \" >}}")).Message);
    }

    /// <summary>Confirms that ref and relref accept named arguments and log a call without a path.</summary>
    [Fact]
    public void RefShouldAcceptNamedArgumentsAndLogMissingPath()
    {
        Assert.Equal("<p>https://example.com/a/index.html#top|#</p>",
            Render("{{< ref path=\"a.md#top\" outputFormat=\"html\" >}}|{{< relref \"\" >}}"));

        LoggerMock.Received().Error("ref/relref shortcode with no path found in {File}", "a.md");
    }

    /// <summary>Confirms the youtube output with the privacy-enhanced host and player options.</summary>
    [Fact]
    public void YoutubeShouldRenderPrivacyEnhancedIframe() =>
        Assert.Equal(
            "<div class=\"v\">\n  <iframe src=\"https://www.youtube-nocookie.com/embed/abc?autoplay=1&amp;mute=1&amp;controls=0&amp;start=5&amp;end=9&amp;loop=1&amp;playlist=abc\" allow=\"accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share\" loading=\"lazy\" referrerpolicy=\"strict-origin-when-cross-origin\" title=\"Clip\"></iframe>\n</div>",
            Render("{{< youtube id=\"abc\" autoplay=true controls=false start=5 end=9 loop=\"true\" class=\"v\" title=\"Clip\" loading=\"lazy\" allowFullScreen=false >}}"));

    /// <summary>Confirms the youtube default responsive wrapper with a positional id.</summary>
    [Fact]
    public void YoutubeShouldAcceptPositionalId()
    {
        var html = Render("{{< youtube abc >}}");

        Assert.Contains("src=\"https://www.youtube-nocookie.com/embed/abc\" allowfullscreen", html, StringComparison.Ordinal);
        Assert.Contains("title=\"YouTube video\"", html, StringComparison.Ordinal);
        Assert.StartsWith("<div style=\"position: relative;", html, StringComparison.Ordinal);
        Assert.Contains("mute=1", Render("{{< youtube id=abc mute=true >}}"), StringComparison.Ordinal);
    }

    /// <summary>Confirms the vimeo output with do-not-track, positional and named forms.</summary>
    [Fact]
    public void VimeoShouldRenderIframe()
    {
        Assert.Equal(
            "<div class=\"c\">\n  <iframe src=\"https://player.vimeo.com/video/123?dnt=1\" allowfullscreen loading=\"eager\" title=\"T\"></iframe>\n</div>",
            Render("{{< vimeo 123 c T >}}"));
        Assert.Contains("title=\"Vimeo video\"", Render("{{< vimeo id=123 >}}"), StringComparison.Ordinal);
    }

    /// <summary>Confirms the instagram blockquote embed.</summary>
    [Fact]
    public void InstagramShouldRenderBlockquote()
    {
        var html = Render("{{< instagram CxOWiQNP2MO >}}");

        Assert.Contains("data-instgrm-permalink=\"https://www.instagram.com/p/CxOWiQNP2MO\"", html, StringComparison.Ordinal);
        Assert.EndsWith("<script async src=\"https://www.instagram.com/embed.js\"></script>", html, StringComparison.Ordinal);
    }

    /// <summary>Confirms that the Liquid built-ins fail without an id.</summary>
    [Theory]
    [InlineData("youtube")]
    [InlineData("vimeo")]
    [InlineData("instagram")]
    public void EmbedsShouldRequireId(string name) =>
        Assert.Equal($"shortcode '{name}': The {name} shortcode requires an id",
            Assert.Throws<ShortcodeException>(() => Render($"{{{{< {name} >}}}}")).Message);

    /// <summary>Confirms the highlight output with the options mapped to data attributes.</summary>
    [Fact]
    public void HighlightShouldRenderCodeBlockWithOptions() =>
        Assert.Equal(
            "<div class=\"highlight\"><pre class=\"language-go line-numbers\" data-linenos=\"table\" data-start=\"10\" data-line=\"2,4-5\" data-line-offset=\"9\" data-line-anchors=\"L\" data-anchor-linenos=\"true\"><code class=\"language-go\">x := &quot;```&quot;\n</code></pre></div>",
            Render("{{< highlight go \"linenos=table,hl_lines=2 4-5,linenostart=10,lineanchors=L,anchorlinenos\" >}}\nx := \"```\"\n{{< /highlight >}}"));

    /// <summary>Confirms the highlight output without options or language.</summary>
    [Fact]
    public void HighlightShouldRenderPlainCode()
    {
        Assert.Equal("<div class=\"highlight\"><pre><code>a &lt; b\n</code></pre></div>",
            Render("{{< highlight >}}a < b{{< /highlight >}}"));
        Assert.Equal("<div class=\"highlight\"><pre class=\"language-js\"><code class=\"language-js\">x\n</code></pre></div>",
            Render("{{< highlight lang=js options=\"linenos=false\" >}}x{{< /highlight >}}"));
        Assert.Empty(HighlightShortcode.ParseOptions(" , =x"));
    }

    /// <summary>Confirms the qr output as an inline PNG with its size and attributes, from text or inner content.</summary>
    [Fact]
    public void QrShouldRenderInlinePng()
    {
        var html = Render("{{< qr text=\"https://example.com\" level=H scale=2 alt=\"Code\" class=\"q\" title=\"T\" loading=\"lazy\" />}}");

        Assert.Matches("^<img src=\"data:image/png;base64,[A-Za-z0-9+/=]+\" width=\"(\\d+)\" height=\"\\1\" alt=\"Code\" class=\"q\" title=\"T\" loading=\"lazy\">$", html);
        Assert.StartsWith("<p>Inline <img src=\"data:image/png;base64,", Render("Inline {{< qr >}}hello{{< /qr >}}"),
            StringComparison.Ordinal);
    }

    /// <summary>Confirms the qr argument errors.</summary>
    [Theory]
    [InlineData("{{< qr />}}", "shortcode 'qr': requires 'text' or inner content")]
    [InlineData("{{< qr text=a scale=0 />}}", "shortcode 'qr': 'scale' must be at least 1")]
    [InlineData("{{< qr text=a level=Z />}}", "shortcode 'qr': 'level' must be one of L, M, Q, H; got 'Z'")]
    public void QrShouldReportInvalidArguments(string markdown, string message) =>
        Assert.Equal(message, Assert.Throws<ShortcodeException>(() => Render(markdown)).Message);

    /// <summary>Confirms that x fetches the oEmbed HTML once and serves it from the disk cache afterwards.</summary>
    [Fact]
    public void XShouldFetchAndCacheOEmbedHtml()
    {
        var cache = Path.Combine(Path.GetTempPath(), $"fanstatic-x-{Guid.NewGuid():N}");
        var http = Substitute.For<IHttpClientWrapper>();
        http.GetAsync(Arg.Any<Uri>()).Returns(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"html\":\"<blockquote>post</blockquote>\"}")
        });
        var shortcode = new XShortcode(http, cache);
        var context = new ShortcodeContext { Name = "x", NamedParams = { ["user"] = "zoo", ["id"] = 123 } };
        Assert.False(shortcode.UsesInner);
        Assert.Empty(shortcode.Positional);
        Assert.StartsWith(Path.GetTempPath(), XShortcode.DefaultCacheDirectory, StringComparison.Ordinal);

        try
        {
            Assert.Equal("<blockquote>post</blockquote>", shortcode.Render(context, Site, Page()));
            Assert.Equal("<blockquote>post</blockquote>", shortcode.Render(context, Site, Page()));

            http.Received(1).GetAsync(Arg.Is<Uri>(uri => uri.ToString().StartsWith(
                "https://publish.x.com/oembed?url=https%3A%2F%2Fx.com%2Fzoo%2Fstatus%2F123&dnt=true",
                StringComparison.Ordinal)));
            Assert.True(File.Exists(Path.Combine(cache, "zoo-123.html")));
        }
        finally
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    /// <summary>Confirms that x falls back to a link and warns when the endpoint fails.</summary>
    [Fact]
    public void XShouldFallBackToLinkWhenOffline()
    {
        var http = Substitute.For<IHttpClientWrapper>();
        http.GetAsync(Arg.Any<Uri>()).ThrowsAsync(new HttpRequestException("offline"));
        var shortcode = new XShortcode(http, Path.Combine(Path.GetTempPath(), $"fanstatic-x-{Guid.NewGuid():N}"));
        var context = new ShortcodeContext { Name = "x", NamedParams = { ["user"] = "a b", ["id"] = "1" } };

        var html = shortcode.Render(context, Site, Page());

        Assert.Equal(
            "<blockquote class=\"twitter-tweet\"><a href=\"https://x.com/a%20b/status/1\">https://x.com/a%20b/status/1</a></blockquote>",
            html);
        LoggerMock.Received().Warning("The x shortcode could not retrieve {Url} for {File}: {Reason}",
            "https://x.com/a%20b/status/1", Arg.Any<string>(), "offline");
        Assert.Equal("requires two named parameters: user and id",
            Assert.Throws<ArgumentException>(() => shortcode.Render(new() { Name = "x" }, Site, Page())).Message);
    }

    Page Page() => new(ContentSourceMock, Site, Site, ("html", null), ["html"]);
}
