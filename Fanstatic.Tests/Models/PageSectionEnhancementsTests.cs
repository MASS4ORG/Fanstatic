using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.Parsers;
using NSubstitute;
using Xunit;

namespace Fanstatic.Test.Models;

/// <summary>Tests for section navigation, summaries, and table-of-contents page members.</summary>
public class PageSectionEnhancementsTests : TestSetup
{
    const string SiteYaml = """
        BaseUrl: https://example.test/
        SummaryLength: 5
        TableOfContents:
          StartLevel: 2
          EndLevel: 3
        """;

    const string HeadingMarkdown = """
        # Title

        ## First **bold**

        ### Child

        ## A & B
        """;

    readonly IFileSystem _fs = new FileSystem();

    /// <summary>Confirms the section hierarchy, first section, and reversed breadcrumb rendering.</summary>
    [Fact]
    public void AncestorsSectionsAndFirstSectionShouldExposeHierarchy()
    {
        using var tempSite = CreateSite(SiteYaml, NavigationFiles());
        var site = tempSite.Site;
        var home = PageByPath(site, "index.md");
        var blog = PageByPath(site, "blog/_index.md");
        var news = PageByPath(site, "news/_index.md");
        var series = PageByPath(site, "blog/series/_index.md");
        var alpha = PageByPath(site, "blog/alpha.md");

        Assert.Equal(["Blog", "News"], SortedTitles(site.Sections));
        Assert.Equal(["Blog", "News"], SortedTitles(home.Sections));
        Assert.Equal(["Series"], SortedTitles(blog.Sections));
        Assert.Equal(["Blog", "Home"], alpha.Ancestors.Select(page => page.Title));
        Assert.Same(blog, alpha.FirstSection);
        Assert.Same(news, news.FirstSection);

        var breadcrumb = site.TemplateEngine.RenderInline(
            "{% for a in page.Ancestors reversed %}{{ a.Title }};{% endfor %}", site, alpha);
        Assert.Equal("Home;Blog;", breadcrumb);

        Assert.Same(alpha.Ancestors, alpha.Ancestors);
        Assert.Same(alpha.FirstSection, alpha.FirstSection);
        Assert.Same(home.Sections, home.Sections);
        Assert.Same(site.Sections, site.Sections);
    }

    /// <summary>Confirms site-wide and top-level section neighbours follow date-descending, title-ascending order.</summary>
    [Fact]
    public void PrevAndNextShouldReturnExpectedNeighboursWithNilAtEnds()
    {
        using var tempSite = CreateSite(SiteYaml, NavigationFiles());
        var site = tempSite.Site;
        var echo = PageByPath(site, "blog/series/echo.md");
        var foxtrot = PageByPath(site, "blog/series/foxtrot.md");
        var about = PageByPath(site, "about.md");
        var charlie = PageByPath(site, "news/charlie.md");
        var alpha = PageByPath(site, "blog/alpha.md");
        var bravo = PageByPath(site, "blog/bravo.md");
        var delta = PageByPath(site, "news/delta.md");

        Assert.Equal(["Echo", "Foxtrot", "About", "Charlie", "Alpha", "Bravo", "Delta"],
            Titles(site.RegularPagesByDateDescendingTitle));
        Assert.Null(echo.Prev);
        Assert.Same(foxtrot, echo.Next);
        Assert.Same(echo, foxtrot.Prev);
        Assert.Same(about, foxtrot.Next);
        Assert.Same(foxtrot, about.Prev);
        Assert.Same(charlie, about.Next);
        Assert.Same(about, charlie.Prev);
        Assert.Same(alpha, charlie.Next);
        Assert.Same(charlie, alpha.Prev);
        Assert.Same(bravo, alpha.Next);
        Assert.Same(alpha, bravo.Prev);
        Assert.Same(delta, bravo.Next);
        Assert.Same(bravo, delta.Prev);
        Assert.Null(delta.Next);

        Assert.Same(PageByPath(site, "blog/_index.md"), echo.FirstSection);
        Assert.Null(echo.PrevInSection);
        Assert.Same(foxtrot, echo.NextInSection);
        Assert.Same(echo, foxtrot.PrevInSection);
        Assert.Same(alpha, foxtrot.NextInSection);
        Assert.Same(foxtrot, alpha.PrevInSection);
        Assert.Same(bravo, alpha.NextInSection);
        Assert.Same(alpha, bravo.PrevInSection);
        Assert.Null(bravo.NextInSection);
        Assert.Null(charlie.PrevInSection);
        Assert.Same(delta, charlie.NextInSection);
        Assert.Null(about.FirstSection);
        Assert.Null(about.PrevInSection);
        Assert.Null(about.NextInSection);
    }

    /// <summary>Confirms all three summary sources, truncation, and reading-time calculation.</summary>
    [Fact]
    public void SummaryTruncatedAndReadingTimeShouldUseExpectedSources()
    {
        SiteSettingsMock.SummaryLength = 5;
        var explicitPage = CreatePage("A long body with many words.",
            new FrontMatter { Summary = "Explicit summary" });
        var markerPage = CreatePage("Intro sentence.\n\n<!--more-->\n\nRest of the article.");
        var wordPage = CreatePage("one two three four five six seven eight");

        Assert.Equal("Explicit summary", explicitPage.Summary);
        Assert.True(explicitPage.Truncated);
        Assert.Equal("Intro sentence.", markerPage.Summary);
        Assert.True(markerPage.Truncated);
        Assert.DoesNotContain("<!--more-->", markerPage.ContentPreRendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<!--more-->", markerPage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("one two three four five", wordPage.Summary);
        Assert.True(wordPage.Truncated);
        Assert.Equal(1, wordPage.ReadingTime);
        Assert.Same(wordPage.Summary, wordPage.Summary);
    }

    /// <summary>Confirms reading time rounds up from the cached word count at 213 words per minute.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(213, 1)]
    [InlineData(214, 2)]
    public void ReadingTimeShouldRoundUpWordsPerMinute(int words, int expected)
    {
        var page = CreatePage(string.Join(' ', Enumerable.Repeat("word", words)));

        Assert.Equal(expected, page.ReadingTime);
    }

    /// <summary>Confirms fragment data and nested table-of-contents HTML match the generated heading ids.</summary>
    [Fact]
    public void FragmentsAndTableOfContentsShouldMatchHeadings()
    {
        var page = CreatePage(HeadingMarkdown);

        Assert.Equal([(1, "Title", "title"), (2, "First bold", "first-bold"),
                         (3, "Child", "child"), (2, "A & B", "a-b")],
            page.Fragments.Headings.Select(heading => (heading.Level, heading.Text, heading.Id)));
        Assert.Equal(["title", "first-bold", "child", "a-b"], page.Fragments.Identifiers);
        Assert.Equal(
            "<nav id=\"TableOfContents\"><ul><li><a href=\"#first-bold\">First bold</a>"
            + "<ul><li><a href=\"#child\">Child</a></li></ul></li>"
            + "<li><a href=\"#a-b\">A &amp; B</a></li></ul></nav>",
            page.TableOfContents);
        Assert.Same(page.Fragments, page.Fragments);
        Assert.Same(page.TableOfContents, page.TableOfContents);

        var rendered = Site.TemplateEngine.RenderInline(
            "{% for id in page.Fragments.Identifiers %}{{ id }};{% endfor %}|{{ page.TableOfContents }}",
            Site, page);
        Assert.Contains("first-bold;", rendered, StringComparison.Ordinal);
        Assert.Contains("<nav id=\"TableOfContents\">", rendered, StringComparison.Ordinal);
    }

    /// <summary>Confirms inline code contributes plain text to collected headings.</summary>
    [Fact]
    public void FragmentsShouldIncludeInlineCodeText()
    {
        var page = CreatePage("## Use `code` here");

        Assert.Equal([(2, "Use code here", "use-code-here")],
            page.Fragments.Headings.Select(heading => (heading.Level, heading.Text, heading.Id)));
    }

    /// <summary>Confirms the table of contents uses the configured heading level range.</summary>
    [Fact]
    public void TableOfContentsShouldHonorConfiguredLevels()
    {
        SiteSettingsMock.TableOfContents = new TableOfContentsSettings { StartLevel = 3, EndLevel = 3 };
        var page = CreatePage(HeadingMarkdown);

        Assert.Equal("<nav id=\"TableOfContents\"><ul><li><a href=\"#child\">Child</a></li></ul></nav>",
            page.TableOfContents);
    }

    static IPage PageByPath(ISite site, string relativePath) =>
        site.Pages.Single(page => page.OutputFormat == "html" && page.SourceRelativePath == relativePath);

    static IReadOnlyList<string> SortedTitles(IEnumerable<IPage> pages) =>
        pages.Select(page => page.Title ?? string.Empty)
            .OrderBy(title => title, StringComparer.Ordinal)
            .ToList();

    static IReadOnlyList<string?> Titles(IEnumerable<IPage> pages) =>
        pages.Select(page => page.Title).ToList();

    Page CreatePage(string rawContent, FrontMatter? frontMatter = null) =>
        new(new(SourcePathConst, frontMatter ?? new FrontMatter(), rawContent), Site, Site, ("html", null), []);

    TempSite CreateSite(string yaml, IReadOnlyDictionary<string, string> files)
    {
        var root = Path.Combine(Path.GetTempPath(), $"fanstatic-enhancements-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(Path.Combine(root, "content"));
        File.WriteAllText(Path.Combine(root, "fanstatic.yaml"), yaml);
        foreach (var (relativePath, content) in files)
        {
            var fullPath = Path.Combine(root, relativePath);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        var options = new GenerateOptions { SourceArgument = root };
        var parser = new YamlParser();
        var settings = SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
        var clock = Substitute.For<ISystemClock>();
        _ = clock.Now.Returns(TodayDate);
        var site = new Site(options, settings, parser, LoggerMock, clock);
        site.ScanAndParseSourceFiles(_fs, site.SourceContentPath);
        site.ProcessPages();
        return new TempSite(root, site);
    }

    static Dictionary<string, string> NavigationFiles() => new()
    {
        ["content/index.md"] = "---\nTitle: Home\n---\nHome\n",
        ["content/about.md"] = "---\nTitle: About\nDate: 2022-01-04\n---\nAbout\n",
        ["content/blog/_index.md"] = "---\nTitle: Blog\n---\nBlog\n",
        ["content/blog/series/_index.md"] = "---\nTitle: Series\n---\nSeries\n",
        ["content/blog/series/echo.md"] = "---\nTitle: Echo\nDate: 2022-01-05\n---\nEcho\n",
        ["content/blog/series/foxtrot.md"] = "---\nTitle: Foxtrot\nDate: 2022-01-05\n---\nFoxtrot\n",
        ["content/blog/alpha.md"] = "---\nTitle: Alpha\nDate: 2022-01-02\n---\nAlpha\n",
        ["content/blog/bravo.md"] = "---\nTitle: Bravo\nDate: 2022-01-02\n---\nBravo\n",
        ["content/news/_index.md"] = "---\nTitle: News\n---\nNews\n",
        ["content/news/charlie.md"] = "---\nTitle: Charlie\nDate: 2022-01-03\n---\nCharlie\n",
        ["content/news/delta.md"] = "---\nTitle: Delta\nDate: 2022-01-01\n---\nDelta\n",
    };

    sealed class TempSite(string root, ISite site) : IDisposable
    {
        public ISite Site { get; } = site;

        public void Dispose() => Directory.Delete(root, recursive: true);
    }
}
