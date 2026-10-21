using Fanstatic.Helpers;
using Fanstatic.Parsers;
using Fanstatic.Shortcodes;
using Fanstatic.Test.Shortcodes;
using NSubstitute;
using Xunit;

namespace Fanstatic.Test.Parser;

/// <summary>
/// Verifies wikilinks across site variants, text rendering, shortcodes and tables.
/// </summary>
public class WikiLinkRenderingTests : TestSetup
{
    /// <summary>Taxonomy links use the calling page's language and output format.</summary>
    [Theory]
    [InlineData("index.md", "html", "tags/sucos", "/tags/sucos/index.html")]
    [InlineData("index.md", "rss", "sucos", "/tags/sucos/index.xml")]
    [InlineData("index.pt-br.md", "html", "sucos", "/pt-br/tags/sucos/index.html")]
    [InlineData("index.pt-br.md", "rss", "tags/sucos", "/pt-br/tags/sucos/index.xml")]
    [InlineData("index.pt-br.md", "rss", "TAG/SUCOS", "/pt-br/tags/sucos/index.xml")]
    public void TaxonomyLinksShouldUseCallerVariant(string source, string format, string target, string expected)
    {
        using var site = CreateSite();

        Assert.Equal(expected, WikiLinkResolver.Resolve(site.Site, site.Page(source, format), target));
    }

    /// <summary>Empty targets use the same missing-link diagnostic as unknown names.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyTargetsShouldReportMissingLink(string? target)
    {
        using var site = CreateSite();

        Assert.Equal("./", WikiLinkResolver.Resolve(site.Site, site.Page("index.md"), target));
        LoggerMock.Received().Error("wikilink: unable to resolve {Target} referenced in {File}",
            target ?? string.Empty, Path.Combine(site.Site.SourceContentPath, "index.md"));
    }

    /// <summary>A root content path resolves to the calling language's home page.</summary>
    [Fact]
    public void RootTargetShouldResolveHome()
    {
        using var site = CreateSite();
        var page = site.Page("index.pt-br.md", "rss");

        Assert.Equal(page.RelPermalink.ToString(), WikiLinkResolver.Resolve(site.Site, page, "/"));
    }

    /// <summary>Plain text contains the label without resolving a URL or emitting HTML.</summary>
    [Theory]
    [InlineData("[[sucos]]", "sucos")]
    [InlineData("[[sucos|the engine]]", "the engine")]
    [InlineData("[[missing|plain text]]", "plain text")]
    public void PlainShouldContainOnlyDisplayText(string markdown, string expected)
    {
        using var site = CreateSite(markdown, strict: true);
        LoggerMock.ClearReceivedCalls();

        Assert.Equal(expected, site.Page("index.md").Plain.Trim());
        Assert.Empty(LoggerMock.ReceivedCalls());
    }

    /// <summary>Image alternative text contains escaped labels without anchor markup.</summary>
    [Theory]
    [InlineData("[[sucos]]", "sucos")]
    [InlineData("[[sucos|the engine]]", "the engine")]
    [InlineData("[[missing|a \"quote\" & <tag>]]", "a &quot;quote&quot; &amp; &lt;tag&gt;")]
    public void ImageAltShouldContainOnlyDisplayText(string link, string expected)
    {
        using var site = CreateSite($"![See {link}](image.png)", strict: true);

        Assert.Equal($"<p><img src=\"image.png\" alt=\"See {expected}\" /></p>",
            site.Page("index.md").ContentPreRendered.Trim());
    }

    /// <summary>Wikilinks inside ordinary link labels render as text while retaining the outer destination.</summary>
    [Theory]
    [InlineData("[See [[sucos]]](https://example.com)", "See sucos")]
    [InlineData("[See [[sucos|the engine]]](https://example.com)", "See the engine")]
    [InlineData("[See *[[sucos]]*](https://example.com)", "See <em>sucos</em>")]
    [InlineData("[See [[missing|a \"quote\" & <tag>]]](https://example.com)",
        "See a &quot;quote&quot; &amp; &lt;tag&gt;")]
    [InlineData("[See [[sucos]]][external]\n\n[external]: https://example.com", "See sucos")]
    [InlineData("[See [[sucos|the engine]]][external]\n\n[external]: https://example.com", "See the engine")]
    [InlineData("[See *[[sucos]]*][external]\n\n[external]: https://example.com", "See <em>sucos</em>")]
    [InlineData("[See [[missing|a \"quote\" & <tag>]]][external]\n\n[external]: https://example.com",
        "See a &quot;quote&quot; &amp; &lt;tag&gt;")]
    public void OuterLinksShouldRenderWikilinksAsText(string markdown, string expected)
    {
        using var site = CreateSite(markdown, strict: true);
        LoggerMock.ClearReceivedCalls();

        Assert.Equal($"<p><a href=\"https://example.com\">{expected}</a></p>",
            site.Page("index.md").ContentPreRendered.Trim());
        Assert.Empty(LoggerMock.ReceivedCalls());
    }

    /// <summary>Brackets without a resolved outer link leave the inner wikilink active.</summary>
    [Theory]
    [InlineData("[See [[sucos]]]", "[See <a href=\"/tags/sucos/index.html\">sucos</a>]")]
    [InlineData("[See [[sucos]]][unknown]", "[See <a href=\"/tags/sucos/index.html\">sucos</a>][unknown]")]
    public void UnresolvedOuterLinksShouldKeepWikilinks(string markdown, string expected)
    {
        using var site = CreateSite(markdown);

        Assert.Equal($"<p>{expected}</p>", site.Page("index.md").ContentPreRendered.Trim());
    }

    /// <summary>Labeled wikilinks consume escaped pipe separators inside table cells.</summary>
    [Theory]
    [InlineData("[[sucos\\|the engine]]", "the engine")]
    [InlineData("[[tags/sucos\\|the engine]]", "the engine")]
    [InlineData("[[sucos\\|a \\\\ path]]", "a \\\\ path")]
    public void TableLinksShouldResolveEscapedSeparator(string link, string label)
    {
        using var site = CreateSite($"| Link | Other |\n| --- | --- |\n| {link} | value |");

        var html = site.Page("index.md").ContentPreRendered;

        Assert.Contains($"<td><a href=\"/tags/sucos/index.html\">{label}</a></td>", html,
            StringComparison.Ordinal);
        Assert.Contains("<td>value</td>", html, StringComparison.Ordinal);
    }

    /// <summary>Markdown rendered by shortcode templates uses the caller's site variant.</summary>
    [Theory]
    [InlineData("{{< details >}}[[sucos]]{{< /details >}}")]
    [InlineData("{{< md >}}[[sucos]]{{< /md >}}")]
    [InlineData("{{% md %}}[[sucos]]{{% /md %}}")]
    public void ShortcodeMarkdownShouldResolveLinks(string markdown)
    {
        using var site = CreateSite(markdown);

        Assert.Contains("<a href=\"/pt-br/tags/sucos/index.xml\">sucos</a>",
            site.Page("index.pt-br.md", "rss").ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Both template rendering entry points supply context to markdownify.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LayoutMarkdownifyShouldResolveLinks(bool inline)
    {
        using var site = CreateSite();
        var page = site.Page("index.pt-br.md", "rss");
        const string template = "{{ '[[sucos]]' | markdownify }}";

        var html = inline
            ? site.Site.TemplateEngine.RenderInline(template, site.Site, page)
            : site.Site.TemplateEngine.Render(template, site.Site, page);

        Assert.Equal("<a href=\"/pt-br/tags/sucos/index.xml\">sucos</a>", html);
        Assert.Null(WikiLinkContext.CurrentScope);
    }

    /// <summary>Shortcodes called from layouts also resolve their Markdown links.</summary>
    [Fact]
    public void LayoutShortcodeShouldResolveLinks()
    {
        using var site = CreateSite();
        var page = site.Page("index.pt-br.md", "rss");

        var html = ShortcodeRenderer.RenderFromTemplate("details", [], "[[sucos]]", site.Site, page);

        Assert.Contains("<a href=\"/pt-br/tags/sucos/index.xml\">sucos</a>", html, StringComparison.Ordinal);
    }

    /// <summary>Missing targets rendered through markdownify retain the normal diagnostic.</summary>
    [Theory]
    [InlineData("{{< details >}}[[missing]]{{< /details >}}")]
    [InlineData("{{< md >}}[[missing]]{{< /md >}}")]
    public void ShortcodeMarkdownShouldLogMissingLinks(string markdown)
    {
        using var site = CreateSite(markdown);

        Assert.Contains("<a href=\"./\">missing</a>", site.Page("index.md").ContentPreRendered,
            StringComparison.Ordinal);
        LoggerMock.Received().Error("wikilink: unable to resolve {Target} referenced in {File}",
            "missing", Path.Combine(site.Site.SourceContentPath, "index.md"));
    }

    /// <summary>Strict missing-link policy also applies inside shortcode Markdown.</summary>
    [Theory]
    [InlineData("{{< details >}}[[missing]]{{< /details >}}")]
    [InlineData("{{< md >}}[[missing]]{{< /md >}}")]
    public void ShortcodeMarkdownShouldRejectMissingLinksInStrictMode(string markdown)
    {
        using var site = CreateSite(markdown, strict: true);

        var exception = Assert.Throws<ShortcodeException>(() => site.Page("index.md").ContentPreRendered);

        Assert.Contains("wikilink: unable to resolve 'missing'", exception.Message, StringComparison.Ordinal);
        Assert.Equal(Path.Combine(site.Site.SourceContentPath, "index.md"), exception.SourcePath);
        Assert.Null(WikiLinkContext.CurrentScope);
    }

    /// <summary>Unclosed, empty and multiline wikilinks remain literal Markdown text.</summary>
    [Theory]
    [InlineData("[[sucos", "[[sucos")]
    [InlineData("[[]]", "[[]]")]
    [InlineData("[[|label]]", "[[|label]]")]
    [InlineData("[[sucos\ntext]]", "[[sucos\ntext]]")]
    [InlineData("`[[sucos]]`", "<code>[[sucos]]</code>")]
    public void NonLinksShouldRemainLiteral(string markdown, string expected)
    {
        using var site = CreateSite(markdown);

        Assert.Equal($"<p>{expected}</p>", site.Page("index.md").ContentPreRendered.Trim());
    }

    ShortcodeTestSite CreateSite(string markdown = "", bool strict = false) =>
        new(LoggerMock, new Dictionary<string, string>
        {
            ["fanstatic.yaml"] = "Title: test\nTheme: test\nbaseURL: https://example.com/\n" +
                                 "DefaultLanguage: en\nLanguages:\n  en:\n    Weight: 1\n  pt-br:\n    Weight: 2\n" +
                                 $"MissingWikiLinkAsError: {strict.ToString().ToLowerInvariant()}\n",
            ["content/index.md"] = "---\ntitle: Home\n---\n" + markdown,
            ["content/index.pt-br.md"] = "---\ntitle: Inicio\n---\n" + markdown,
            ["content/posts/hello.md"] = "---\ntitle: Hello\nTags: [sucos]\n---\nHello\n",
            ["content/posts/hello.pt-br.md"] = "---\ntitle: Ola\nTags: [sucos]\n---\nOla\n",
            ["shortcodes/md.html"] = "{{ inner | markdownify }}"
        });
}
