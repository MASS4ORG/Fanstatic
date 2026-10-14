using System.Globalization;
using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.TemplateEngine;
using NSubstitute;
using Xunit;

namespace Fanstatic.Test.Models;

/// <summary>Unit tests for the Page class.</summary>
public class PageTests : TestSetup
{
    const string Markdown1Const =
        """
        # word01 word02

        word03 word04 word05 6 7 eight

        ## nine

        ```cs
        console.WriteLine('hello word')
        ```
        """;

    const string Markdown2Const =
        """
        # word01 word02

        word03 word04 word05 6 7 [eight](https://example.com)
        """;

    const string MarkdownPlain1Const =
        """
        word01 word02
        word03 word04 word05 6 7 eight
        nine
        console.WriteLine('hello word')

        """;

    const string MarkdownPlain2Const =
        """
        word01 word02
        word03 word04 word05 6 7 eight

        """;

    [Theory]
    [InlineData(TitleConst, SourcePathConst, "file", "/path/to")]
    /// <summary>Confirms that a page exposes the title, source path, site, and file name values from its content source.</summary>
    public void FrontMatterShouldCreateWithCorrectProperties(string title,
        string sourcePath, string sourceFileNameWithoutExtension,
        string sourcePathDirectory)
    {
        ArgumentNullException.ThrowIfNull(sourcePathDirectory);
        var page = new Page(ContentSourceMock, Site, Site, ("html", null), []);
        var expectedDirectory = Path.Combine(" ", Path.Combine(sourcePathDirectory.Split("/"))).TrimStart();

        // Assert
        Assert.Equal(title, page.Title);
        Assert.Equal(sourcePath, page.SourceRelativePath);
        Assert.Same(Site, page.Site);
        Assert.Equal(sourceFileNameWithoutExtension, page.SourceFileNameWithoutExtension);
        Assert.Equal(expectedDirectory, page.SourceRelativePathDirectory);
    }

    [Fact]
    /// <summary>Confirms that a page without optional front matter values exposes the expected defaults.</summary>
    public void FrontMatterShouldHaveDefaultValuesForOptionalProperties()
    {
        // Arrange
        var page = new Page(ContentSourceMock, Site, Site, ("html", null), []);
        var relPermalink = new Uri("", UriKind.RelativeOrAbsolute);

        // Assert
        Assert.Equal(string.Empty, page.Section);
        Assert.Equal(Kind.single, page.Kind);
        Assert.Equal("page", page.Type);
        Assert.Null(page.Url);
        Assert.Empty(page.Params);
        Assert.Null(page.Date);
        Assert.Null(page.LastMod);
        Assert.Null(page.PublishDate);
        Assert.Null(page.ExpiryDate);
        Assert.Null(page.AliasesProcessed);
        Assert.Equal(page.RelPermalink, relPermalink);
        Assert.Contains(relPermalink, page.AllOutputUrLs);
        Assert.Single(page.AllOutputUrLs);
        Assert.Equal(string.Empty, page.RawContent);
        Assert.Empty(page.TagsReference);
        Assert.Empty(page.PagesReferences);
        Assert.Empty(page.RegularPages);
        Assert.False(Site.IsDateExpired(page));
        Assert.True(Site.IsDatePublishable(page));
    }

    [Theory]
    [InlineData("/v123/index.html")]
    [InlineData("/test-title-2/index.html")]
    /// <summary>Confirms that front matter aliases are parsed into URLs registered in the output references.</summary>
    public void AliasesShouldParseAsUrls(string url)
    {
        var page = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = TitleConst,
            Aliases = ["v123", "{{ page.Title }}", "{{ page.Title }}-2"]
        }, string.Empty), Site, Site, ("html", null), []);

        // Act
        Site.PostProcessPage(page);
        Uri uri = new(url, UriKind.RelativeOrAbsolute);

        // Assert
        Assert.Equal(3, Site.OutputReferences.Count);
        _ = Site.OutputReferences.TryGetValue(uri, out var pageOther);
        Assert.NotNull(pageOther);
        Assert.Same(page, pageOther);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    /// <summary>Confirms that a page is expired only when its expiry date has passed.</summary>
    public void IsDateExpiredShouldReturnExpectedResult(int days,
        bool expected)
    {
        var page = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = TitleConst,
            ExpiryDate = SystemClockMock.Now.AddDays(days)
        }, string.Empty), Site, Site, ("html", null), []);

        // Assert
        Assert.Equal(expected, Site.IsDateExpired(page));
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(null, "2024-01-01", false)]
    [InlineData("2022-01-01", null, true)]
    [InlineData("2024-01-01", "2022-01-01", false)]
    [InlineData("2022-01-01", "2024-01-01", true)]
    /// <summary>Confirms that publishability follows the publish date and date relative to the clock.</summary>
    public void IsDatePublishableShouldReturnCorrectValues(string? publishDate,
        string? date, bool expectedValue)
    {
        var page = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = TitleConst,
            PublishDate = publishDate is null ? null : DateTime.Parse(publishDate, CultureInfo.InvariantCulture),
            Date = date is null ? null : DateTime.Parse(date, CultureInfo.InvariantCulture)
        }, string.Empty),
            Site, Site, ("html", null), []);

        // Assert
        Assert.Equal(expectedValue, Site.IsDatePublishable(page));
    }

    [Theory]
    // Draft as null
    [InlineData(null, null, null, false, true)]
    [InlineData(null, "2024-01-01", null, false, false)]
    [InlineData("2022-01-01", null, null, false, true)]
    [InlineData("2024-01-01", "2022-01-01", null, false, false)]
    [InlineData("2022-01-01", "2024-01-01", null, false, true)]
    // Draft as false
    [InlineData(null, null, false, false, true)]
    [InlineData(null, "2024-01-01", false, false, false)]
    [InlineData("2022-01-01", null, false, false, true)]
    [InlineData("2024-01-01", "2022-01-01", false, false, false)]
    [InlineData("2022-01-01", "2024-01-01", false, false, true)]
    // Draft as true
    [InlineData(null, null, true, false, false)]
    [InlineData(null, "2024-01-01", true, false, false)]
    [InlineData("2022-01-01", null, true, false, false)]
    [InlineData("2024-01-01", "2022-01-01", true, false, false)]
    [InlineData("2022-01-01", "2024-01-01", true, false, false)]
    // Draft as null, option -d
    [InlineData(null, null, null, true, true)]
    [InlineData(null, "2024-01-01", null, true, false)]
    [InlineData("2022-01-01", null, null, true, true)]
    [InlineData("2024-01-01", "2022-01-01", null, true, false)]
    [InlineData("2022-01-01", "2024-01-01", null, true, true)]
    // Draft as false, option -d
    [InlineData(null, null, false, true, true)]
    [InlineData(null, "2024-01-01", false, true, false)]
    [InlineData("2022-01-01", null, false, true, true)]
    [InlineData("2024-01-01", "2022-01-01", false, true, false)]
    [InlineData("2022-01-01", "2024-01-01", false, true, true)]
    // Draft as true, option -d
    [InlineData(null, null, true, true, true)]
    [InlineData(null, "2024-01-01", true, true, false)]
    [InlineData("2022-01-01", null, true, true, true)]
    [InlineData("2024-01-01", "2022-01-01", true, true, false)]
    [InlineData("2022-01-01", "2024-01-01", true, true, true)]
    /// <summary>Confirms that page validity follows publish date, date, draft flag, and the draft option.</summary>
    public void IsValidPageShouldReturnCorrectValues(string? publishDate,
        string? date, bool? draft, bool draftOption, bool expectedValue)
    {
        var page = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = TitleConst,
            PublishDate = publishDate is null ? null : DateTime.Parse(publishDate, CultureInfo.InvariantCulture),
            Date = date is null ? null : DateTime.Parse(date, CultureInfo.InvariantCulture),
            Draft = draft
        }, string.Empty),
            Site, Site, ("html", null), []);

        var options = Substitute.For<IGenerateOptions>();
        _ = options.Draft.Returns(draftOption);

        // Assert
        Assert.Equal(expectedValue, Site.IsPageValid(page, options));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    /// <summary>Confirms that a page with a future date is valid only when the future option is enabled.</summary>
    public void IsValidDateShouldReturnExpectedResult(bool futureOption,
        bool expected)
    {
        var page = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = TitleConst,
            Date = SystemClockMock.Now.AddDays(1)
        }, string.Empty), Site, Site, ("html", null), []);

        // Act
        var options = Substitute.For<IGenerateOptions>();
        _ = options.Future.Returns(futureOption);

        // Assert
        Assert.Equal(expected, Site.IsDateValid(page, options));
    }

    [Theory]
    [InlineData("/test/path/index.md", "/test-title/index.html")]
    [InlineData("/another/path/index.md", "/test-title/index.html")]
    /// <summary>Confirms that a page permalink is derived from its title when no URL is specified.</summary>
    public void CreatePermalinkShouldReturnCorrectUrlWhenUrlIsNull(
        string sourcePath, string expectedUrl)
    {
        var page = new Page(new(sourcePath, new FrontMatter
        {
            Title = TitleConst,
        }, string.Empty), Site, Site, ("html", null), []);

        // Assert
        Assert.Equal(new(expectedUrl, UriKind.RelativeOrAbsolute), Site.CreatePermalink(page));
    }

    [Theory]
    [InlineData(null, "/test-title/index.html")]
    [InlineData("{{ page.Title }}/{{ page.SourceFileNameWithoutExtension }}",
        "/test-title/file/index.html")]
    /// <summary>Confirms that a page permalink uses the default template or a custom URL template.</summary>
    public void PermalinkCreateWithDefaultOrCustomURLTemplate(
        string? urlTemplate, string expectedPermalink)
    {
        var page = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = TitleConst,
            Url = urlTemplate
        }, string.Empty), Site, Site, ("html", null), []);
        var actualPermalink = Site.CreatePermalink(page);

        // Assert
        Assert.Equal(new(expectedPermalink, UriKind.RelativeOrAbsolute), actualPermalink);
    }

    [Theory]
    [InlineData(Kind.single, true)]
    [InlineData(Kind.list, false)]
    /// <summary>Confirms that only pages of kind single are included in regular pages.</summary>
    public void RegularPagesShouldReturnCorrectPagesWhenKindIsSingle(
        Kind kind, bool isExpectedPage)
    {
        var page = new Page(new(SourcePathConst, new()
        {
            Title = TitleConst,
        }, string.Empty)
        {
            Kind = kind
        },
            Site, Site, ("html", null), []);

        // Act
        Site.PostProcessPage(page);

        // Assert
        Assert.Equal(isExpectedPage, Site.RegularPages.Contains(page));
    }

    [Theory]
    [InlineData(Markdown1Const, 13)]
    [InlineData(Markdown2Const, 8)]
    /// <summary>Confirms that the word count reflects the markdown content and that the plain text is cached.</summary>
    public void WordCountShouldReturnCorrectCounts(string rawContent,
        int wordCountExpected)
    {
        var page = new Page(new(SourcePathConst, new(), rawContent), Site, Site, ("html", null), []);

        Parallel.For(0, 1_000, _ => Assert.Equal(wordCountExpected, page.WordCount));
        Assert.Same(page.Plain, page.Plain);
    }

    [Theory]
    [InlineData(Markdown1Const, MarkdownPlain1Const)]
    [InlineData(Markdown2Const, MarkdownPlain2Const)]
    /// <summary>Confirms that the plain text rendering of markdown content matches the expected string and is cached.</summary>
    public void PlainShouldReturnCorrectPlainString(string rawContent,
        string plain)
    {
        ArgumentException.ThrowIfNullOrEmpty(plain);
        var page = new Page(new(SourcePathConst, new(), rawContent), Site, Site, ("html", null), []);
        // Required to make the test pass on Windows
        plain = plain.Replace("\r\n", "\n", StringComparison.Ordinal);

        // Assert
        Assert.Equal(plain, page.Plain);
        Assert.Same(page.Plain, page.Plain);
    }

    [Fact]
    /// <summary>Confirms that a page template is rendered only once even across repeated content access.</summary>
    public void ContentShouldRenderTemplateOncePerPage()
    {
        var templateEngine = Substitute.For<ITemplateEngine>();
        var site = Substitute.For<ISite>();
        var children = Enumerable.Range(1, 3)
            .Select(index => new Page(
                new($"content-{index}.md", new FrontMatter(), $"Content {index}"),
                Site,
                Site,
                ("html", null),
                [])
            {
                SiteInternal = site
            })
            .ToList();
        var parent = new Page(new("index.md", new FrontMatter(), string.Empty), Site, Site, ("html", null), [])
        {
            SiteInternal = site
        };
        site.TemplateEngine.Returns(templateEngine);
        site.ParseAndRenderTemplate(Arg.Any<Page>(), false)
            .Returns(call =>
            {
                var page = call.Arg<Page>();
                if (ReferenceEquals(page, parent))
                {
                    foreach (var child in children)
                    {
                        _ = child.Content;
                        _ = child.Content;
                    }

                    return "Parent output";
                }

                return templateEngine.Render("content", site, page);
            });
        templateEngine.Render(Arg.Any<string>(), site, Arg.Any<IPage>(), Arg.Any<int?>())
            .Returns(call => ((IPage)call[2]!).SourceRelativePath);

        Assert.Equal("Parent output", parent.Content);
        Assert.Equal("Parent output", parent.Content);
        foreach (var child in children)
        {
            templateEngine.Received(1).Render("content", site, child, Arg.Any<int?>());
        }
    }

    [Fact]
    /// <summary>Confirms that concurrent circular content rendering fails with a recursive rendering error instead of deadlocking.</summary>
    public void ContentShouldAvoidDeadlockForConcurrentPageCycles()
    {
        var site = Substitute.For<ISite>();
        var initialRenders = new Barrier(2);
        var renderCounts = new int[2];
        var first = new Page(new("first.md", new FrontMatter(), "first"), Site, Site, ("html", null), [])
        {
            SiteInternal = site
        };
        var second = new Page(new("second.md", new FrontMatter(), "second"), Site, Site, ("html", null), [])
        {
            SiteInternal = site
        };
        site.ParseAndRenderTemplate(Arg.Any<Page>(), false)
            .Returns(call =>
            {
                var page = call.Arg<Page>();
                var isFirst = ReferenceEquals(page, first);
                var renderCount = Interlocked.Increment(ref renderCounts[isFirst ? 0 : 1]);
                if (renderCount == 1 && !initialRenders.SignalAndWait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Both page renders did not start concurrently.");
                }

                return isFirst ? second.Content : first.Content;
            });

        var exceptions = new Exception?[2];
        var firstThread = new Thread(() => exceptions[0] = Record.Exception(() => _ = first.Content))
        {
            IsBackground = true
        };
        var secondThread = new Thread(() => exceptions[1] = Record.Exception(() => _ = second.Content))
        {
            IsBackground = true
        };
        firstThread.Start();
        secondThread.Start();

        var firstCompleted = firstThread.Join(TimeSpan.FromSeconds(5));
        var secondCompleted = secondThread.Join(TimeSpan.FromSeconds(5));
        Assert.True(firstCompleted && secondCompleted, "Concurrent content cycles must not deadlock.");
        foreach (var exception in exceptions)
        {
            var cycle = Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains("Recursive content rendering", cycle.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    // [InlineData("/pages/page-01", 3)]
    // [InlineData("/pages/page-01/page-01", 3)]
    // [InlineData("/blog/blog-01", 1)]
    // [InlineData("/blog/blog-01/blog-01", 1)]
    // [InlineData("/index/post-01", 1)]
    [InlineData("/index/post-01/post-01/index.html", 2)]
    // [InlineData("/articles/article-01", 0)]
    /// <summary>Confirms that nested pages inherit the cascaded weight from their ancestor sections.</summary>
    public void CascadeShouldCascadeValues(string url, int weight)
    {
        GenerateOptions options = new()
        {
            SourceArgument = Path.GetFullPath(Path.Combine(TestSitesPath, TestSitePathConst09))
        };
        Site.Options = options;

        // Act
        Site.ScanAndParseSourceFiles(new FileSystem());
        Site.ProcessPages();
        Site.OutputReferences.TryGetValue(new(url, UriKind.RelativeOrAbsolute), out var itemPage);
        var page = itemPage as Page;

        // Assert
        Assert.Equal(weight, page!.Weight);
    }

    [Theory]
    [InlineData("/index/post-01/index.html", "cascade")]
    [InlineData("/index/post-01/post-01/index.html", "own")]
    /// <summary>Confirms that nested pages inherit or override cascaded params from their ancestor sections.</summary>
    public void CascadeShouldCascadeParams(string url, string? valueString)
    {
        GenerateOptions options = new()
        {
            SourceArgument = Path.GetFullPath(Path.Combine(TestSitesPath, TestSitePathConst09))
        };
        Site.Options = options;
        Uri uri = new(url, UriKind.RelativeOrAbsolute);

        // Act
        Site.ScanAndParseSourceFiles(new FileSystem());
        Site.ProcessPages();
        Site.OutputReferences.TryGetValue(uri, out var itemPage);
        var page = itemPage as Page;
        Assert.NotNull(itemPage);

        // Assert
        Assert.Equal(valueString, page!.Params["valueString"]);
    }

    [Fact]
    /// <summary>Confirms that the default template lookup order contains all expected combinations.</summary>
    public void TemplateLookupShouldDefaultGenerateAllCombinations()
    {
        var page = new Page(new(string.Empty, new(), string.Empty), Site, Site,
            ("html", null), []);

        // Act
        var paths = page.GetTemplateLookupOrder(false);

        // Assert
        Assert.Equal(6, paths.Count());
    }

    [Theory]
    [InlineData(Kind.taxonomy, "taxonomy")]
    [InlineData(Kind.term, "term")]
    /// <summary>Confirms that taxonomy and term pages prefer kind-specific templates over defaults.</summary>
    public void TemplateLookupShouldPreferTaxonomySpecificTemplates(Kind kind, string template)
    {
        var content = new ContentSource("categories/_index.md",
            new FrontMatter { Section = "categories" }, string.Empty)
        {
            Kind = kind
        };
        var page = new Page(content, Site, Site, ("html", null), []);

        var paths = page.GetTemplateLookupOrder(false).ToList();

        Assert.Equal(Path.Combine("categories", $"{template}.html"), paths[0]);
        Assert.Equal(Path.Combine("_default", $"{template}.html"), paths[1]);
    }

    [Theory]
    [InlineData("", "", Kind.single, 4)]
    [InlineData("page", "", Kind.single, 6)]
    [InlineData("", "blog", Kind.single, 8)]
    [InlineData("page", "blog", Kind.single, 12)]
    [InlineData("post", "", Kind.home, 30)]
    [InlineData("post", "blog", Kind.section, 36)]
    /// <summary>Confirms that the template lookup order includes every combination of type, section, and kind.</summary>
    public void TemplateLookupShouldGenerateAllCombinations(string type,
        string section, Kind kind, int expectedCount)
    {
        FrontMatter frontMatter = new()
        {
            Type = type,
            Section = section
        };
        ContentSource content = new(string.Empty, new(), string.Empty)
        {
            FrontMatter = frontMatter,
            Kind = kind
        };
        var page = new Page(content, Site, Site, ("html", null), []);

        // Act
        var paths = page.GetTemplateLookupOrder(false).ToList();

        // Assert
        Assert.Equal(expectedCount, paths.Count());
        Assert.Contains(Path.Combine("_default", kind.ToString()), paths);
        Assert.Contains(Path.Combine("_default", $"{kind}.html"), paths);
        if (!string.IsNullOrEmpty(type))
        {
            Assert.Contains(Path.Combine(type, kind.ToString()), paths);
            Assert.Contains(Path.Combine(type, $"{kind}.html"), paths);
        }
    }

    [Fact]
    /// <summary>Confirms that the permalink combines the base URL with the relative permalink.</summary>
    public void PermalinkShouldReturnFullUrlWithBaseUrl()
    {
        // Arrange
        (Site as ISiteSettings).BaseUrl = new("https://example.com");
        var page = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = TitleConst,
        }, string.Empty), Site, Site, ("html", null), [])
        {
            RelPermalink = new("/test-title/index.html", UriKind.RelativeOrAbsolute)
        };

        // Act
        var permalink = (page as IOutput).Permalink;

        // Assert
        Assert.Equal(new("https://example.com/test-title/index.html"), permalink);
    }

    [Theory]
    [InlineData("https://example.com/", "/test-title/index.html", "https://example.com/test-title/")]
    [InlineData("https://example.com:8908/", "/blog/post/index.html", "https://example.com:8908/blog/post/")]
    [InlineData("http://example.com/", "/index.html", "http://example.com/")]
    /// <summary>Confirms that the permalink directory is derived from the base URL and relative permalink.</summary>
    public void PermalinkDirShouldReturnCorrectDirectory(string baseUrl, string relPermalink, string expectedDir)
    {
        // Arrange
        Site = new Site(GenerateOptionsMock, new()
        {
            BaseUrl = new(baseUrl),
        }, FrontMatterParser, LoggerMock, SystemClockMock);
        var page = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = TitleConst,
        }, string.Empty), Site, Site, ("html", null), [])
        {
            RelPermalink = new(relPermalink, UriKind.RelativeOrAbsolute)
        };

        // Act
        var permalinkDir = (page as IOutput).PermalinkDir;

        // Assert
        Assert.Equal(new(expectedDir, UriKind.RelativeOrAbsolute), permalinkDir);
    }

    [Theory]
    [InlineData("/test-title/index.html", "index.html")]
    [InlineData("/blog/post/article.html", "article.html")]
    /// <summary>Confirms that the permalink filename is the last segment of the relative permalink.</summary>
    public void PermalinkFilenameShouldReturnCorrectFilename(
        string relPermalink, string expectedFilename)
    {
        // Arrange
        Site = new Site(GenerateOptionsMock, new()
        {
            BaseUrl = new("https://example.com"),
        }, FrontMatterParser, LoggerMock, SystemClockMock);
        var page = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = TitleConst,
        }, string.Empty), Site, Site, ("html", null), [])
        {
            RelPermalink = new(relPermalink, UriKind.RelativeOrAbsolute)
        };

        // Act
        var permalinkFilename = (page as IOutput).PermalinkFilename;

        // Assert
        Assert.Equal(expectedFilename, permalinkFilename);
    }

    [Theory]
    [InlineData("/test-title/index.html", "/test-title")]
    [InlineData("/blog/post/index.html", "/blog/post")]
    [InlineData("/index.html", "/")]
    /// <summary>Confirms that the relative permalink directory is derived from the relative permalink.</summary>
    public void RelPermalinkDirShouldReturnCorrectDirectory(
        string relPermalink, string expectedDir)
    {
        // Arrange
        var page = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = TitleConst,
        }, string.Empty), Site, Site, ("html", null), [])
        {
            RelPermalink = new(relPermalink, UriKind.RelativeOrAbsolute)
        };

        // Act
        var relPermalinkDir = (page as IOutput).RelPermalinkDir;

        // Assert
        Assert.Equal(new(expectedDir, UriKind.RelativeOrAbsolute), relPermalinkDir);
    }

    [Theory]
    [InlineData("/test-title/index.html", "index.html")]
    [InlineData("/blog/post/article.html", "article.html")]
    /// <summary>Confirms that the relative permalink filename is the last segment of the relative permalink.</summary>
    public void RelPermalinkFilenameShouldReturnCorrectFilename(
        string relPermalink, string expectedFilename)
    {
        var page = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = TitleConst,
        }, string.Empty), Site, Site, ("html", null), []);

        // Arrange
        page.RelPermalink = new(relPermalink, UriKind.RelativeOrAbsolute);

        // Act
        var relPermalinkFilename = (page as IOutput).RelPermalinkFilename;

        // Assert
        Assert.Equal(expectedFilename, relPermalinkFilename);
    }
}
