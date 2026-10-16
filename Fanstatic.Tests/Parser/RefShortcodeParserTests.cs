using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Models;
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

    /// <summary>Confirms that content without a ref shortcode is left unchanged.</summary>
    [Fact]
    public void ContentWithoutRefShortcodeShouldBeUnaffected()
    {
        var hello = Page("posts/hello.md");

        Assert.Contains("Hello content.", hello.ContentPreRendered, StringComparison.Ordinal);
    }
}
