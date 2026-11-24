using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.Parsers;
using NSubstitute;
using Xunit;

namespace Fanstatic.Test.Parser;

/// <summary>
/// Tests for the Hugo-style <c>ref</c>/<c>relref</c> shortcode resolution in markdown content.
/// </summary>
public class RefShortcodeParserTests : TestSetup
{
    const string SitePath = ".TestSites/14-ref-shortcode";

    readonly Site _site;

    /// <summary>Initializes the fixture and builds the ref shortcode sample site.</summary>
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

    /// <summary>Confirms that ref resolves to the target page's absolute permalink.</summary>
    [Fact]
    public void RefShouldResolveToAbsolutePermalink()
    {
        var about = Page("about.md");
        var hello = Page("posts/hello.md");

        Assert.Contains($"href=\"{hello.Permalink}\"", about.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that relref resolves to the target page's relative permalink.</summary>
    [Fact]
    public void RelrefShouldResolveToRelativePermalink()
    {
        var about = Page("about.md");
        var hello = Page("posts/hello.md");

        Assert.Contains($"href=\"{hello.RelPermalink}\"", about.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that a ref with an anchor appends the fragment to the resolved link.</summary>
    [Fact]
    public void RefWithAnchorShouldAppendFragment()
    {
        var about = Page("about.md");
        var hello = Page("posts/hello.md");

        Assert.Contains($"{hello.Permalink}#section", about.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that a ref with a lang argument resolves to that translation.</summary>
    [Fact]
    public void RefWithLangArgumentShouldResolveToTranslation()
    {
        var about = Page("about.md");
        var helloPt = Page("posts/hello.pt-br.md");

        Assert.Contains(helloPt.Permalink.ToString(), about.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that a ref with an output format argument resolves to that format.</summary>
    [Fact]
    public void RefWithOutputFormatArgumentShouldResolveToThatFormat()
    {
        var about = Page("about.md");
        var helloRss = Page("posts/hello.md", "rss");

        Assert.Contains(helloRss.Permalink.ToString(), about.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that a ref to a missing translation falls back to the default language.</summary>
    [Fact]
    public void RefWithMissingTranslationShouldFallBackToDefaultLanguage()
    {
        var about = Page("about.md");
        var greeting = Page("posts/greeting.md");
        var greetingDe = Page("posts/greeting.de.md");

        Assert.DoesNotContain(greetingDe.Permalink.ToString(), about.ContentPreRendered, StringComparison.Ordinal);
        Assert.Contains(greeting.Permalink.ToString(), about.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that an unresolvable ref falls back to a hash link.</summary>
    [Fact]
    public void RefWithUnresolvablePathShouldFallBackToHash()
    {
        var about = Page("about.md");

        Assert.Contains("Missing: #", about.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that an unresolvable ref logs the absolute source path.</summary>
    [Fact]
    public void RefWithUnresolvablePathShouldLogTheAbsoluteSourcePath()
    {
        var about = Page("about.md");

        _ = about.ContentPreRendered;

        LoggerMock.Received().Error("ref/relref: unable to resolve {Path} referenced in {File}",
            "posts/does-not-exist.md", Path.Combine(_site.SourceContentPath, "about.md"));
    }

    /// <summary>Confirms that a root-relative content path resolves from the content root.</summary>
    [Fact]
    public void RefWithRootRelativePathShouldResolveFromContentRoot()
    {
        var about = Page("about.md");
        var hello = Page("posts/hello.md");

        Assert.Contains($"RootPath: {hello.Permalink}", about.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that a path starting with the content directory resolves from the content root.</summary>
    [Fact]
    public void RefWithContentDirectoryPathShouldResolveFromContentRoot()
    {
        var about = Page("about.md");
        var hello = Page("posts/hello.md");

        Assert.Contains($"ContentPath: {hello.Permalink}", about.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that ref and relref convert a page resource path to its final image URLs.</summary>
    [Fact]
    public void RefToPageResourceShouldUseFinalResourceUrls()
    {
        var about = Page("about.md");
        var cover = _site.OutputReferences.Values.OfType<IResource>()
            .Single(resource => resource.SourceRelativePath == "posts/gallery/cover.jpg");

        Assert.Contains($"ResourcePath: {cover.Permalink}", about.ContentPreRendered, StringComparison.Ordinal);
        Assert.Contains($"ResourceRelPath: {cover.RelPermalink}", about.ContentPreRendered, StringComparison.Ordinal);
        Assert.Contains($"ResourceContentPath: {cover.Permalink}", about.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that bundle index paths resolve to their collapsed page path.</summary>
    [Fact]
    public void RefToBundleIndexShouldResolveToItsPage()
    {
        var about = Page("about.md");

        Assert.Contains($"BundleIndex: {Page("posts/gallery/index.md").Permalink}", about.ContentPreRendered,
            StringComparison.Ordinal);
        Assert.Contains($"BundleBranch: {Page("posts/_index.md").Permalink}", about.ContentPreRendered,
            StringComparison.Ordinal);
        Assert.Contains($"Home: {_site.Home!.Permalink}", about.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that the caller-relative path wins over a content-root path with the same name.</summary>
    [Fact]
    public void RefShouldPreferCallerRelativePathOverContentRootPath()
    {
        var greeting = Page("posts/greeting.md");
        var postsHello = Page("posts/hello.md");
        var rootHello = Page("hello.md");

        Assert.Contains($"CallerFirst: {postsHello.Permalink}", greeting.ContentPreRendered, StringComparison.Ordinal);
        Assert.DoesNotContain($"CallerFirst: {rootHello.Permalink}", greeting.ContentPreRendered,
            StringComparison.Ordinal);
    }

    /// <summary>Confirms that a leading slash selects the content-root path over the caller-relative one.</summary>
    [Fact]
    public void RootRelativeRefShouldSkipCallerRelativePath()
    {
        var greeting = Page("posts/greeting.md");
        var rootHello = Page("hello.md");

        Assert.Contains($"RootFirst: {rootHello.Permalink}", greeting.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that content without a ref shortcode is left unchanged.</summary>
    [Fact]
    public void ContentWithoutRefShortcodeShouldBeUnaffected()
    {
        var hello = Page("posts/hello.md");

        Assert.Contains("Hello content.", hello.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that an anchor without a path resolves to the calling page.</summary>
    [Fact]
    public void AnchorWithoutPathShouldResolveToCallingPage()
    {
        var about = Page("about.md");

        Assert.Contains($"{about.Permalink}#section", about.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that a resource renamed through front matter resolves to its final URL.</summary>
    [Fact]
    public void RefToRenamedResourceShouldUseFinalUrl()
    {
        var gallery = Page("posts/gallery/index.md");
        var cover = Cover();

        Assert.Contains($"OwnResource: {cover.Permalink}", gallery.ContentPreRendered, StringComparison.Ordinal);
        Assert.Contains($"RenamedResource: {cover.Permalink}", gallery.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that anchors append to resource and root-relative relref links.</summary>
    [Fact]
    public void AnchorsShouldAppendToResourceAndRootRelativeLinks()
    {
        var about = Page("about.md");
        var cover = Cover();
        var hello = Page("posts/hello.md");

        Assert.Contains($"ResourceAnchor: {cover.Permalink}#figure", about.ContentPreRendered,
            StringComparison.Ordinal);
        Assert.Contains($"RelRootAnchor: {hello.RelPermalink}#root", about.ContentPreRendered,
            StringComparison.Ordinal);
    }

    /// <summary>Confirms that a root slash resolves to the home page.</summary>
    [Fact]
    public void RootSlashShouldResolveToHome()
    {
        var about = Page("about.md");

        Assert.Contains($"HomeRoot: {_site.Home!.Permalink}", about.ContentPreRendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms that resource refs fall back from the requested language and output format.</summary>
    [Fact]
    public void ResourceRefsShouldFallBackToAvailableVariants()
    {
        var about = Page("about.md");
        var cover = Cover();

        Assert.Contains($"ResourceLangFallback: {cover.Permalink}", about.ContentPreRendered,
            StringComparison.Ordinal);
        Assert.Contains($"ResourceFormatFallback: {cover.Permalink}", about.ContentPreRendered,
            StringComparison.Ordinal);
    }

    /// <summary>Confirms that a nearer resource does not hide a page for wikilinks.</summary>
    [Fact]
    public void WikilinkShouldSkipResourceAndResolvePage()
    {
        var gallery = Page("posts/gallery/index.md");
        var notes = Page("notes.md");

        Assert.Equal(notes.RelPermalink.ToString(), WikiLinkResolver.Resolve(_site, gallery, "notes.md"));
    }

    IResource Cover() => _site.OutputReferences.Values.OfType<IResource>()
        .Single(resource => resource.SourceRelativePath == "posts/gallery/cover.jpg");
}
