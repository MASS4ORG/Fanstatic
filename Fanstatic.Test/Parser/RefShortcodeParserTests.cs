using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Xunit;

namespace Fanstatic.Test.Parser;

/// <summary>
/// Tests for the Hugo-style <c>ref</c>/<c>relref</c> shortcode resolution in markdown content.
/// </summary>
public class RefShortcodeParserTests : TestSetup
{
    const string SitePath = ".TestSites/14-ref-shortcode";

    readonly Site _site;

    public RefShortcodeParserTests()
    {
        var fs = new FileSystem();
        var sitePath = Path.GetFullPath(Path.Combine(TestSitesPath, SitePath));
        var options = new GenerateOptions { SourceArgument = sitePath };
        var settings = SiteHelper.ParseSettings("fanstatic.yaml", options, FrontMatterParser, fs);

        _site = new Site(options, settings, FrontMatterParser, LoggerMock, SystemClockMock);
        _site.ResetCache();
        _site.ScanAndParseSourceFiles(fs, _site.SourceContentPath);
        _site.BuildTranslationGroups();
        _site.ProcessPages();
    }

    IPage Page(string sourceRelativePath, string outputFormat = "html") =>
        _site.OutputReferences.Values
            .OfType<Page>()
            .First(page => page.SourceRelativePath == sourceRelativePath
                           && page.OutputFormat == outputFormat
                           && page.PageIndex == 1);

    [Fact]
    public void Ref_ShouldResolveToAbsolutePermalink()
    {
        var about = Page("about.md");
        var hello = Page("posts/hello.md");

        Assert.Contains($"href=\"{hello.Permalink}\"", about.ContentPreRendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Relref_ShouldResolveToRelativePermalink()
    {
        var about = Page("about.md");
        var hello = Page("posts/hello.md");

        Assert.Contains($"href=\"{hello.RelPermalink}\"", about.ContentPreRendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Ref_WithAnchor_ShouldAppendFragment()
    {
        var about = Page("about.md");
        var hello = Page("posts/hello.md");

        Assert.Contains($"{hello.Permalink}#section", about.ContentPreRendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Ref_WithLangArgument_ShouldResolveToTranslation()
    {
        var about = Page("about.md");
        var helloPt = Page("posts/hello.pt-br.md");

        Assert.Contains(helloPt.Permalink.ToString(), about.ContentPreRendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Ref_WithOutputFormatArgument_ShouldResolveToThatFormat()
    {
        var about = Page("about.md");
        var helloRss = Page("posts/hello.md", "rss");

        Assert.Contains(helloRss.Permalink.ToString(), about.ContentPreRendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Ref_WithUnresolvablePath_ShouldFallBackToHash()
    {
        var about = Page("about.md");

        Assert.Contains("Missing: #", about.ContentPreRendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Content_WithoutRefShortcode_ShouldBeUnaffected()
    {
        var hello = Page("posts/hello.md");

        Assert.Contains("Hello content.", hello.ContentPreRendered, StringComparison.Ordinal);
    }
}
