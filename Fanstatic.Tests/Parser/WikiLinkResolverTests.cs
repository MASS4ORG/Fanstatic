using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Models;
using NSubstitute;
using Xunit;

namespace Fanstatic.Test.Parser;

/// <summary>
/// Tests for Obsidian-style <c>[[wikilink]]</c> resolution in markdown content.
/// </summary>
public class WikiLinkResolverTests : TestSetup
{
    const string SitePath = ".TestSites/18-wikilinks";

    readonly Site _site;

    /// <summary>Initializes the fixture and builds the wikilink sample site.</summary>
    public WikiLinkResolverTests()
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

    /// <summary>Confirms that a content-path wikilink resolves to the target page.</summary>
    [Fact]
    public void WikilinkWithPathShouldResolveToPage()
    {
        var home = Page("index.md");
        var hello = Page("posts/hello.md");

        Assert.Contains($"<a href=\"{hello.RelPermalink}\">posts/hello.md</a>", home.ContentPreRendered,
            StringComparison.Ordinal);
    }

    /// <summary>Confirms that a taxonomy-prefixed wikilink resolves to the term page.</summary>
    [Fact]
    public void WikilinkWithTaxonomyPrefixShouldResolveToTerm()
    {
        var home = Page("index.md");

        Assert.Contains("<a href=\"/tags/sucos/index.html\">tags/sucos</a>", home.ContentPreRendered,
            StringComparison.Ordinal);
    }

    /// <summary>Confirms that a bare wikilink resolves to a matching taxonomy term.</summary>
    [Fact]
    public void BareWikilinkShouldResolveToTerm()
    {
        var home = Page("index.md");

        Assert.Contains("<a href=\"/tags/sucos/index.html\">sucos</a>", home.ContentPreRendered,
            StringComparison.Ordinal);
    }

    /// <summary>Confirms that the label after a pipe is used as the link text.</summary>
    [Fact]
    public void WikilinkWithLabelShouldUseLabelAsText()
    {
        var home = Page("index.md");

        Assert.Contains("<a href=\"/tags/sucos/index.html\">the engine</a>", home.ContentPreRendered,
            StringComparison.Ordinal);
    }

    /// <summary>Confirms that an unresolvable wikilink falls back to <c>./</c>.</summary>
    [Fact]
    public void UnresolvableWikilinkShouldFallBackToDotSlash()
    {
        var home = Page("index.md");

        Assert.Contains("<a href=\"./\">does-not-exist</a>", home.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that an unresolvable wikilink logs an error.</summary>
    [Fact]
    public void UnresolvableWikilinkShouldLogAnError()
    {
        _ = Page("index.md").ContentPreRendered;

        LoggerMock.Received().Error("wikilink: unable to resolve {Target} referenced in {File}",
            "does-not-exist", Path.Combine(_site.SourceContentPath, "index.md"));
    }

    /// <summary>Confirms that a wikilink at the very end of the content still resolves.</summary>
    [Fact]
    public void WikilinkAtEndOfContentShouldResolve()
    {
        var home = Page("index.md");

        Assert.Contains("Line-end: <a href=\"/tags/sucos/index.html\">sucos</a>", home.ContentPreRendered,
            StringComparison.Ordinal);
    }

    /// <summary>Confirms that two adjacent wikilinks both resolve.</summary>
    [Fact]
    public void AdjacentWikilinksShouldBothResolve()
    {
        var home = Page("index.md");

        Assert.Contains("Adjacent: <a href=\"/tags/sucos/index.html\">sucos</a>"
                        + "<a href=\"/tags/sucos/index.html\">tags/sucos</a>",
            home.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that content without a wikilink is left unchanged.</summary>
    [Fact]
    public void ContentWithoutWikilinkShouldBeUnaffected()
    {
        var hello = Page("posts/hello.md");

        Assert.Contains("Hello content.", hello.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that <c>MissingWikiLinkAsError: true</c> fails the build on an unresolved wikilink.</summary>
    [Fact]
    public void MissingWikiLinkShouldThrowWhenConfiguredAsError()
    {
        var site = BuildSite(".TestSites/19-wikilinks-error");
        var home = site.OutputReferences.Values
            .OfType<Page>()
            .First(page => page.SourceRelativePath == "index.md" && page.OutputFormat == "html" && page.PageIndex == 1);

        var exception = Assert.Throws<InvalidOperationException>(() => _ = home.ContentPreRendered);
        Assert.Contains("does-not-exist", exception.Message, StringComparison.Ordinal);
    }

    Site BuildSite(string relativePath)
    {
        var fs = new FileSystem();
        var sitePath = Path.GetFullPath(Path.Combine(TestSitesPath, relativePath));
        var options = new GenerateOptions { SourceArgument = sitePath };
        var settings = SiteHelper.ParseSettings("fanstatic.yaml", options, FrontMatterParser, fs);

        var site = new Site(options, settings, FrontMatterParser, LoggerMock, SystemClockMock);
        site.ResetCache();
        site.ScanAndParseSourceFiles(fs, site.SourceContentPath);
        site.BuildTranslationGroups();
        site.ProcessPages();
        return site;
    }
}
