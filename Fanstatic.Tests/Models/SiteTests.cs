using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.Parsers;
using NSubstitute;
using Xunit;

namespace Fanstatic.Test.Models;

/// <summary>
/// Unit tests for the Site class.
/// </summary>
public class SiteTests : TestSetup
{
    readonly IFileSystem _fs = new FileSystem();

    /// <summary>Confirms that scanning all markdown files registers pages whose file names match the source files.</summary>
    [Theory]
    [InlineData("test01.md")]
    [InlineData("date-ok.md")]
    public void ScanAllMarkdownFilesShouldContainFilenames(string fileName)
    {
        var fileNameWithoutExtension =
            Path.GetFileNameWithoutExtension(fileName);
        var siteFullPath =
            Path.GetFullPath(Path.Combine(TestSitesPath, TestSitePathConst01));
        Site.Options = new GenerateOptions
        {
            SourceArgument = siteFullPath
        };

        // Act
        Site.ScanAndParseSourceFiles(_fs,
            Path.Combine(siteFullPath, "content"));
        Site.ProcessPages();

        // Assert
        Assert.Contains(Site.Pages,
            page => page.SourceRelativePathDirectory.Length == 0);
        Assert.Contains(Site.Pages,
            page => page.SourceFileNameWithoutExtension ==
                    fileNameWithoutExtension);
    }

    /// <summary>Confirms that each test site exposes a single home page marked as home.</summary>
    [Theory]
    [InlineData(TestSitePathConst01)]
    [InlineData(TestSitePathConst02)]
    public void HomeShouldReturnAHomePage(string sitePath)
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath, sitePath))
        };
        Site.Options = options;

        // Act
        Site.ScanAndParseSourceFiles(_fs, Site.SourceContentPath);
        Site.ProcessPages();

        // Assert
        Assert.NotNull(Site.Home);
        Assert.True(Site.Home.IsHome);
        Assert.Single(Site.OutputReferences.Values, output => output is IPage
        {
            IsHome: true
        });
    }

    /// <summary>Confirms that invalid front matter is reported as a parsing error while valid pages continue to be processed.</summary>
    [Fact]
    public void ScanAndParseSourceFilesShouldReportInvalidFrontMatterWithoutStackTraceAndKeepGoing()
    {
        var source = Path.Combine(Path.GetTempPath(), $"fanstatic-invalid-{Guid.NewGuid():N}");
        var content = Path.Combine(source, "content");
        Directory.CreateDirectory(content);
        File.WriteAllText(Path.Combine(source, "fanstatic.yaml"), "BaseUrl: https://example.test/\n");
        File.WriteAllText(Path.Combine(content, "bad.md"), "---\ntitle: bad\naliases: single\n---\nbad\n");
        File.WriteAllText(Path.Combine(content, "good.md"), "---\ntitle: good\n---\ngood\n");

        try
        {
            GenerateOptions options = new() { SourceArgument = source };
            var parser = new YamlParser();
            var settings = SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
            var site = new Site(options, settings, parser, LoggerMock, SystemClockMock);

            site.ScanAndParseSourceFiles(_fs, site.SourceContentPath);
            site.ProcessPages();

            LoggerMock.Received(1).Error("Error parsing file {File}: {Reason}",
                Path.Combine(content, "bad.md"), Arg.Is<string>(reason => reason.Contains("front matter line")));
            Assert.Contains(site.Pages, page => page.SourceRelativePath == "good.md");
            Assert.DoesNotContain(site.Pages, page => page.SourceRelativePath == "bad.md");
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    /// <summary>Confirms that duplicate permalinks are reported with the absolute paths of both source files.</summary>
    [Fact]
    public void ProcessPagesShouldReportDuplicatePermalinksWithAbsoluteSourcePaths()
    {
        var source = Path.Combine(Path.GetTempPath(), $"fanstatic-duplicate-{Guid.NewGuid():N}");
        var content = Path.Combine(source, "content");
        Directory.CreateDirectory(content);
        File.WriteAllText(Path.Combine(source, "fanstatic.yaml"), "BaseUrl: https://example.test/\n");
        File.WriteAllText(Path.Combine(content, "a.md"), "---\ntitle: a\nurl: same\n---\na\n");
        File.WriteAllText(Path.Combine(content, "b.md"), "---\ntitle: b\nurl: same\n---\nb\n");

        try
        {
            GenerateOptions options = new() { SourceArgument = source };
            var parser = new YamlParser();
            var settings = SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
            var site = new Site(options, settings, parser, LoggerMock, SystemClockMock);

            site.ScanAndParseSourceFiles(_fs, site.SourceContentPath);
            site.ProcessPages();

            LoggerMock.Received().Error(
                "Duplicate RelPermalink '{Permalink}' from `{File}`. It is already from '{From}'",
                Arg.Any<Uri>(),
                Path.Combine(content, "b.md"),
                Path.Combine(content, "a.md"));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    /// <summary>Confirms that pages are linked in path order and inherit cascaded weight and params through section and term pages.</summary>
    [Fact]
    public void ScanAndParseSourceFilesShouldLinkPagesInPathOrderAndPreserveCascade()
    {
        var source = Path.Combine(Path.GetTempPath(), $"fanstatic-ordered-{Guid.NewGuid():N}");
        var content = Path.Combine(source, "content");
        var blog = Path.Combine(content, "blog");
        Directory.CreateDirectory(blog);
        File.WriteAllText(Path.Combine(source, "fanstatic.yaml"), "BaseUrl: https://example.test/\n");
        File.WriteAllText(Path.Combine(blog, "_index.md"),
        "---\ncascade:\n  weight: 7\n  params:\n    inherited: inherited-value\n---\n");
        foreach (var name in new[] { "z", "m", "a" })
        {
            File.WriteAllText(Path.Combine(blog, name + ".md"),
                $"---\ntitle: {name}\ntags: [common]\n---\n{name}\n");
        }

        try
        {
            GenerateOptions options = new() { SourceArgument = source };
            var parser = new YamlParser();
            var settings = SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
            var site = new Site(options, settings, parser, LoggerMock, SystemClockMock);

            site.ScanAndParseSourceFiles(_fs, site.SourceContentPath);
            site.ProcessPages();

            var section = site.Pages.FirstOrDefault(page => page.SourceRelativePath == "blog/_index.md");
            Assert.NotNull(section);
            var children = section.RegularPages
                .Where(page => page.SourceRelativePath != "blog/_index.md")
                .ToList();
            Assert.Equal(
                new[] { "blog/a.md", "blog/m.md", "blog/z.md" },
                children.Select(page => page.SourceRelativePath));
            foreach (var child in children)
            {
                Assert.Equal(7, child.Weight);
                Assert.Equal("inherited-value", child.Params["inherited"]);
            }

            var tag = site.OutputReferences.Values
                .OfType<IPage>()
                .FirstOrDefault(page => page.Kind == Kind.term
                                        && page.OutputFormat == "html"
                                        && page.Title == "common");
            Assert.NotNull(tag);
            Assert.Equal(
                new[] { "blog/a.md", "blog/m.md", "blog/z.md" },
                tag.RegularPages.Select(page => page.SourceRelativePath));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    /// <summary>Confirms that branch bundles render as list pages while leaf bundles stay single pages.</summary>
    [Fact]
    public void ScanAndParseSourceFilesShouldTreatBranchIndexesAsListPages()
    {
        var source = Path.Combine(Path.GetTempPath(), $"fanstatic-branch-{Guid.NewGuid():N}");
        var content = Path.Combine(source, "content");
        Directory.CreateDirectory(Path.Combine(content, "blog", "sub"));
        Directory.CreateDirectory(Path.Combine(content, "blog", "leaf"));
        File.WriteAllText(Path.Combine(source, "fanstatic.yaml"), "BaseUrl: https://example.test/\n");
        File.WriteAllText(Path.Combine(content, "blog", "_index.md"), "---\ntitle: Blog\n---\n");
        File.WriteAllText(Path.Combine(content, "blog", "sub", "_index.md"), "---\ntitle: Sub\n---\n");
        File.WriteAllText(Path.Combine(content, "blog", "leaf", "index.md"), "---\ntitle: Leaf\n---\n");

        try
        {
            GenerateOptions options = new() { SourceArgument = source };
            var parser = new YamlParser();
            var settings = SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
            var site = new Site(options, settings, parser, LoggerMock, SystemClockMock);

            site.ScanAndParseSourceFiles(_fs, site.SourceContentPath);
            site.ProcessPages();

            var blog = site.Pages.First(page => page.SourceRelativePath == "blog/_index.md");
            Assert.Equal(Kind.section, blog.Kind);
            Assert.False(blog.IsPage);
            Assert.Equal(Kind.list, site.Pages.First(page => page.SourceRelativePath == "blog/sub/_index.md").Kind);
            Assert.Equal(Kind.single, site.Pages.First(page => page.SourceRelativePath == "blog/leaf/index.md").Kind);
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    /// <summary>Confirms that the number of section pages matches the expected count for each test site.</summary>
    [Theory]
    [InlineData(TestSitePathConst01, 0)]
    [InlineData(TestSitePathConst02, 0)]
    [InlineData(TestSitePathConst03, 1)]
    public void PageIsSectionShouldReturnExpectedQuantityOfPages(
        string sitePath, int expectedQuantity)
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath, sitePath))
        };
        Site.Options = options;

        // Act
        Site.ScanAndParseSourceFiles(_fs);
        Site.ProcessPages();

        // Assert
        Assert.Equal(expectedQuantity, Site.OutputReferences.Values.Count(output => output is IPage
        {
            Kind: Kind.section,
            OutputFormat: "html"
        }));
    }

    /// <summary>Confirms that the number of referenced HTML pages matches the expected count for each test site.</summary>
    [Theory]
    [InlineData(TestSitePathConst01, 8)]
    [InlineData(TestSitePathConst02, 11)]
    [InlineData(TestSitePathConst03, 16)]
    [InlineData(TestSitePathConst04, 29)]
    public void PagesReferenceShouldReturnExpectedQuantityOfPages(
        string sitePath, int expectedQuantity)
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath, sitePath))
        };
        Site.Options = options;

        // Act
        Site.ScanAndParseSourceFiles(_fs);
        Site.ProcessPages();

        // Assert
        Assert.Equal(expectedQuantity, Site.OutputReferences.Values
            .Count(output => output is IPage
            {
                OutputFormat: "html"
            }));
    }

    /// <summary>Confirms that the number of regular HTML pages matches the expected count for each test site.</summary>
    [Theory]
    [InlineData(TestSitePathConst01, 4)]
    [InlineData(TestSitePathConst02, 7)]
    [InlineData(TestSitePathConst03, 11)]
    [InlineData(TestSitePathConst04, 21)]
    public void PageIsPageShouldReturnExpectedQuantityOfPages(string sitePath,
        int expectedQuantity)
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath, sitePath))
        };
        Site.Options = options;

        // Act
        Site.ScanAndParseSourceFiles(_fs);
        Site.ProcessPages();

        // Assert
        Assert.Equal(expectedQuantity, Site.OutputReferences.Values.Count(output => output is IPage
        {
            IsPage: true,
            OutputFormat: "html"
        }));
    }

    /// <summary>Confirms that pages are ordered by weight from the highest to the lowest value.</summary>
    [Fact]
    public void PageWeightShouldReturnTheRightOrder()
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath,
                    TestSitePathConst03))
        };
        Site.Options = options;

        // Act
        Site.ScanAndParseSourceFiles(_fs);
        Site.ProcessPages();

        // Assert
        Assert.Equal(100, Site.RegularPages.First().Weight);
        Assert.Equal(-100, Site.RegularPages.Last().Weight);
    }

    /// <summary>Confirms that pages without a weight default to a zero weight.</summary>
    [Fact]
    public void PageWeightShouldReturnZeroWeight()
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath,
                    TestSitePathConst01))
        };
        Site.Options = options;

        // Act
        Site.ScanAndParseSourceFiles(_fs);
        Site.ProcessPages();

        // Assert
        Assert.Equal(0, Site.RegularPages.First().Weight);
        Assert.Equal(0, Site.RegularPages.Last().Weight);
    }

    /// <summary>Confirms that page collection properties return cached materialized lists across repeated access.</summary>
    [Fact]
    public void PageCollectionsShouldCacheMaterializedLists()
    {
        GenerateOptions options = new()
        {
            SourceArgument = Path.GetFullPath(Path.Combine(TestSitesPath, TestSitePathConst03))
        };
        var parser = new YamlParser();
        var settings = SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
        var site = new Site(options, settings, parser, LoggerMock, SystemClockMock);

        site.ScanAndParseSourceFiles(_fs, site.SourceContentPath);
        site.ProcessPages();

        Assert.Same(site.Pages, site.Pages);
        Assert.Same(site.RegularPages, site.RegularPages);
        var page = site.Pages.First();
        Assert.Same(page.RegularPages, page.RegularPages);
    }

    /// <summary>Confirms that site and section page views are ordered by date and weight and cached across accesses.</summary>
    [Fact]
    public void SortedPageViewsShouldBeOrderedAndCached()
    {
        GenerateOptions options = new()
        {
            SourceArgument = Path.GetFullPath(Path.Combine(TestSitesPath, TestSitePathConst03))
        };
        var parser = new YamlParser();
        var settings = SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
        var site = new Site(options, settings, parser, LoggerMock, SystemClockMock);

        site.ScanAndParseSourceFiles(_fs, site.SourceContentPath);
        site.ProcessPages();

        var siteOutput = Assert.IsType<SiteOutput>(site.RegularPages.First().Site);
        var pagesByDate = siteOutput.RegularPagesByDate;
        Assert.Same(pagesByDate, siteOutput.RegularPagesByDate);
        Assert.Equal(
            pagesByDate.OrderBy(page => page.Date).Select(page => page.SourceRelativePath),
            pagesByDate.Select(page => page.SourceRelativePath));
        Assert.Same(siteOutput.RegularPagesByLastMod, siteOutput.RegularPagesByLastMod);
        Assert.Same(siteOutput.RegularPagesByWeight, siteOutput.RegularPagesByWeight);
        Assert.Same(siteOutput.RegularPagesByTitle, siteOutput.RegularPagesByTitle);

        var section = site.Pages.FirstOrDefault(page =>
            page.Section == "blog" && page.RegularPages.Any());
        Assert.NotNull(section);
        var sectionPagesByWeight = section.RegularPagesByWeight;
        Assert.Same(sectionPagesByWeight, section.RegularPagesByWeight);
        Assert.Equal(
            section.RegularPages.OrderBy(page => page.Weight).Select(page => page.SourceRelativePath),
            sectionPagesByWeight.Select(page => page.SourceRelativePath));
        Assert.Same(section.PagesByDate, section.PagesByDate);
        Assert.Same(section.PagesByLastMod, section.PagesByLastMod);
        Assert.Same(section.PagesByTitle, section.PagesByTitle);
        Assert.Same(section.RegularPagesByDate, section.RegularPagesByDate);
        Assert.Same(section.RegularPagesByLastMod, section.RegularPagesByLastMod);
        Assert.Same(section.RegularPagesByTitle, section.RegularPagesByTitle);
    }

    /// <summary>Confirms that regular pages sorted by title match the Liquid sort order for mixed-case titles.</summary>
    [Fact]
    public void RegularPagesByTitleShouldMatchLiquidSortForMixedCaseTitles()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var options = new GenerateOptions { SourceArgument = Path.GetTempPath() };
        var settings = new SiteSettings { BaseUrl = new("https://example.test/") };
        var site = new Site(options, settings, new YamlParser(), LoggerMock, SystemClockMock);
        var siteOutput = new SiteOutput(site, ("html", settings.DefaultLanguage));
        var titles = new[] { "alpha", "Bravo", "ALPHA", "bravo" };
        for (var index = 0; index < titles.Length; index++)
        {
            var source = new ContentSource($"{index}.md", new FrontMatter { Title = titles[index] }, titles[index])
            {
                Language = settings.DefaultLanguage
            };
            var outputPage = new Page(source, site, siteOutput, ("html", settings.DefaultLanguage), [])
            {
                RelPermalink = new Uri($"/{index}/", UriKind.Relative)
            };
            site.OutputReferences.TryAdd(outputPage.RelPermalink, outputPage);
        }

        var page = site.RegularPages.First();
        var rendered = site.TemplateEngine.RenderInline(
            "{% assign sorted = site.RegularPages | sort: 'Title' %}"
            + "{% for item in sorted %}{{ item.Title }}\n{% endfor %}",
            site,
            page);
        var liquidSortedTitles = rendered.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(liquidSortedTitles, siteOutput.RegularPagesByTitle.Select(item => item.Title));
        Assert.Contains("ALPHA", liquidSortedTitles);
        Assert.Contains("alpha", liquidSortedTitles);
        Assert.Contains("Bravo", liquidSortedTitles);
        Assert.Contains("bravo", liquidSortedTitles);
    }

    /// <summary>Confirms that page collections are not cached while resources are being rendered.</summary>
    [Fact]
    public void ProcessPagesShouldNotCacheCollectionsDuringResourceRendering()
    {
        var siteFullPath = Path.GetFullPath(Path.Combine(TestSitesPath, TestSitePathConst11));
        GenerateOptions options = new() { SourceArgument = siteFullPath };
        var parser = new YamlParser();
        var settings = SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
        var site = new Site(options, settings, parser, LoggerMock, SystemClockMock);
        site.TemplateEngine.Initialize(site);

        var contentSource = new ContentSource("customized/index.md",
            new FrontMatter
            {
                Title = "Customized",
                ResourceDefinitions =
                [
                    new FrontMatterResources
                    {
                        Src = "*.webp",
                        Name = "{{ site.Pages | size }}-{{ site.RegularPages | size }}-rendered"
                    }
                ]
            }, string.Empty)
        {
            BundleType = BundleType.Leaf
        };
        contentSource.ScanForResources(site);
        Assert.Single(contentSource.RawResources!);
        site.ContentSourceAdd(contentSource);

        site.ProcessPages();

        var page = Assert.IsType<Page>(site.RegularPages.First(page =>
            page.SourceRelativePath == "customized/index.md" && page.OutputFormat == "html"));
        Assert.Contains(page.Resources!, resource =>
            resource.RelPermalink.ToString().EndsWith("0-0-rendered.webp", StringComparison.Ordinal));
    }

    /// <summary>Confirms that pager-generated virtual pages are excluded from page and regular page collections.</summary>
    [Fact]
    public void PageCollectionsShouldExcludeVirtualPages()
    {
        var siteFullPath = Path.GetFullPath(Path.Combine(TestSitesPath, ".TestSites/12-taxonomies"));
        GenerateOptions options = new()
        {
            SourceArgument = siteFullPath
        };
        var site = SiteHelper.Init(
            "fanstatic.yaml",
            options,
            new YamlParser(),
            LoggerMock,
            new StopwatchReporter(LoggerMock),
            _fs);

        var pages = site.Pages;
        var regularPages = site.RegularPages;
        var termPage = Assert.IsAssignableFrom<IPage>(
            site.OutputReferences[new Uri("/tags/release/index.html", UriKind.Relative)]);

        _ = site.TemplateEngine.RenderInline(
            "{% assign pager = page.RegularPages | paginate: 1 %}",
            site,
            termPage);

        var virtualPage = Assert.IsType<Page>(site.OutputReferences.Values
            .First(page => page is Page { PageIndex: > 1 }));
        Assert.DoesNotContain(virtualPage, pages);
        Assert.DoesNotContain(virtualPage, regularPages);

        var sourceTermPage = Assert.IsType<Page>(termPage);
        _ = sourceTermPage.Content;
        var virtualContent = virtualPage.Content;

        Assert.Equal(1, sourceTermPage.Paginator?.Current);
        Assert.Equal(2, virtualPage.Paginator?.Current);
        Assert.Contains(sourceTermPage.Paginator!.PageItems[0].Title!,
            sourceTermPage.Content, StringComparison.Ordinal);
        Assert.Contains(virtualPage.Paginator!.PageItems[0].Title!,
            virtualContent, StringComparison.Ordinal);
        Assert.NotEqual(sourceTermPage.Content, virtualContent);
        Assert.Same(virtualContent, virtualPage.Content);
    }

    /// <summary>Confirms that resetting the cache clears the page and regular page collection caches.</summary>
    [Fact]
    public void ResetCacheShouldClearPageCollectionCaches()
    {
        GenerateOptions options = new()
        {
            SourceArgument = Path.GetFullPath(Path.Combine(TestSitesPath, TestSitePathConst03))
        };
        var parser = new YamlParser();
        var settings = SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
        var site = new Site(options, settings, parser, LoggerMock, SystemClockMock);

        site.ScanAndParseSourceFiles(_fs, site.SourceContentPath);
        site.ProcessPages();
        var pages = site.Pages;
        var regularPages = site.RegularPages;
        var page = pages.First();
        var regularPage = regularPages.First();

        site.ResetCache();
        site.OutputReferences.TryAdd(page.RelPermalink, page);
        site.OutputReferences.TryAdd(regularPage.RelPermalink, regularPage);

        Assert.NotSame(pages, site.Pages);
        Assert.NotSame(regularPages, site.RegularPages);
        Assert.Contains(page, site.Pages);
        Assert.Contains(regularPage, site.RegularPages);
    }

    /// <summary>Confirms that the tags section page exposes the expected number of pages and regular pages.</summary>
    [Fact]
    public void TagSectionPagePagesShouldReturnNumberTagPages()
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath,
                    TestSitePathConst04))
        };
        Site.Options = options;

        // Act
        Site.ScanAndParseSourceFiles(_fs);
        Site.ProcessPages();

        // Assert
        Site.OutputReferences.TryGetValue(
            new("/tags/index.html", UriKind.RelativeOrAbsolute),
            out var output);
        var tagSectionPage = output as IPage;
        Assert.NotNull(tagSectionPage);
        Assert.Equal(10, tagSectionPage.Pages.Count());
        Assert.Equal(10, tagSectionPage.RegularPages.Count());
        Assert.Equal("tags/_index.md", tagSectionPage.SourceRelativePath);
        Assert.Equal("tags", tagSectionPage.SourceRelativePathDirectory);
        Assert.Equal("tags", tagSectionPage.SourcePathLastDirectory);
    }

    /// <summary>Confirms that a term page exposes the expected number of pages and regular pages.</summary>
    [Fact]
    public void TagPagePagesShouldReturnNumberReferences()
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath,
                    TestSitePathConst04))
        };
        Site.Options = options;

        // Act
        Site.ScanAndParseSourceFiles(_fs);
        Site.ProcessPages();

        // Assert
        _ = Site.OutputReferences.TryGetValue(new("/tags/tag1/index.html", UriKind.RelativeOrAbsolute),
            out var output);
        var page = output as IPage;
        Assert.NotNull(page);
        Assert.Equal(10, page.Pages.Count());
        Assert.Equal(10, page.RegularPages.Count());
    }

    /// <summary>Confirms that configured taxonomies build term pages and expose taxonomy and term data to templates.</summary>
    [Fact]
    public void ConfiguredTaxonomiesShouldBuildTermsAndExposeTemplateData()
    {
        var source = Path.GetFullPath(Path.Combine(TestSitesPath, ".TestSites/12-taxonomies"));
        var options = new GenerateOptions { SourceArgument = source };
        var parser = new YamlParser();
        var settings = SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
        var site = new Site(options, settings, parser, LoggerMock, SystemClockMock);

        site.ScanAndParseSourceFiles(_fs, site.SourceContentPath);
        site.ProcessPages();

        var post = Assert.Single(site.RegularPages,
            page => page.Title == "First post" && page.OutputFormat == "html");
        Assert.Single(post.Taxonomies["tags"]);
        Assert.Single(post.Taxonomies["categories"]);
        Assert.Equal("The Trilogy", post.Taxonomies["series"].Single().Title);
        Assert.Same(post.Taxonomies, post.Taxonomies);
        Assert.Single(post.TagsReference);
        Assert.Same(post.TagsReference, post.TagsReference);

        var term = Assert.IsType<Page>(site.OutputReferences[
            new Uri("/series/trilogy/index.html", UriKind.RelativeOrAbsolute)]);
        Assert.Equal("A three-book story.", term.Description);
        Assert.Equal(7, term.Weight);
        Assert.Equal(2, term.RegularPages.Count());

        var siteOutput = post.Site;
        Assert.Same(siteOutput.Taxonomies, siteOutput.Taxonomies);
        Assert.Same(((ISiteOutput)site).Taxonomies, ((ISiteOutput)site).Taxonomies);
        var trilogy = Assert.Single(siteOutput.Taxonomies["series"], t => t.Name == "trilogy");
        Assert.Equal(["trilogy", "another", "duology"],
            siteOutput.Taxonomies["series"].Select(t => t.Name));
        Assert.Equal(["another", "duology", "trilogy"],
            siteOutput.Taxonomies["series"].ByName.Select(t => t.Name));
        Assert.Equal(["trilogy", "another", "duology"],
            siteOutput.Taxonomies["series"].ByCount.Select(t => t.Name));
        Assert.Equal("trilogy", trilogy.Name);
        Assert.Equal(2, trilogy.Count);
        Assert.Equal(2, trilogy.Pages.Count);

        var seriesPage = Assert.IsAssignableFrom<IPage>(site.OutputReferences[
            new Uri("/series/index.html", UriKind.RelativeOrAbsolute)]);
        Assert.Equal("series", seriesPage.Title);
        var renderedTerms = site.TemplateEngine.RenderInline(
            "{% for term in site.Taxonomies[page.Section] %}{{ term.Name }}{% endfor %}",
            site, seriesPage);
        Assert.Equal("trilogyanotherduology", renderedTerms);

        var renderedTermsByName = site.TemplateEngine.RenderInline(
            "{% assign termsByName = site.Taxonomies.series | sort: 'Name' %}"
            + "{% for term in termsByName %}{{ term.Name }}{% endfor %}",
            site, seriesPage);
        Assert.Equal("anotherduologytrilogy", renderedTermsByName);
    }

    /// <summary>Confirms that an empty taxonomy configuration disables generated taxonomy and term pages.</summary>
    [Fact]
    public void EmptyTaxonomyConfigurationShouldDisableGeneratedTaxonomyPages()
    {
        var options = new GenerateOptions
        {
            SourceArgument = Path.GetFullPath(Path.Combine(TestSitesPath, TestSitePathConst04))
        };
        var site = new Site(options, new SiteSettings { Taxonomies = [] },
            FrontMatterParser, LoggerMock, SystemClockMock);

        site.ScanAndParseSourceFiles(_fs, site.SourceContentPath);
        site.ProcessPages();

        Assert.DoesNotContain(site.Pages, page => page.Kind is Kind.taxonomy or Kind.term);
    }

    /// <summary>Confirms that page content matches the expected output when no theme is configured.</summary>
    [Theory]
    [InlineData("/index.html", "<p>Index Content</p>\n")]
    [InlineData("/blog/index.html", "")]
    [InlineData("/tags/index.html", "")]
    [InlineData("/tags/tag1/index.html", "")]
    [InlineData("/blog/test-content-1/index.html", "<p>Test Content 1</p>\n")]
    public void PageContentShouldReturnNullThemeContent(string url,
        string expectedContent)
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath,
                    TestSitePathConst04))
        };
        Site.Options = options;

        // Act
        Site.ScanAndParseSourceFiles(_fs);
        Site.ProcessPages();

        // Assert
        _ = Site.OutputReferences.TryGetValue(
            new(url, UriKind.RelativeOrAbsolute), out var output);
        var page = output as IPage;
        Assert.NotNull(page);
        Assert.Equal(expectedContent, page.Content);
        Assert.Equal(page.ContentPreRendered, page.Content);
    }

    /// <summary>Confirms that content and complete content are produced from a baseof layout when no theme is configured.</summary>
    [Theory]
    [InlineData("/index.html",
        "<p>Index Content</p>\n",
        "INDEX-<p>Index Content</p>\n")]
    [InlineData("/blog/index.html",
        "",
        "LIST-")]
    [InlineData("/tags/index.html",
        "",
        "LIST-")]
    [InlineData("/tags/tag1/index.html",
        "",
        "LIST-")]
    [InlineData("/blog/test-content-1/index.html",
        "<p>Test Content 1</p>\n",
        "SINGLE-<p>Test Content 1</p>\n")]
    public void PageContentShouldReturnNullThemeBaseofContent(string url,
        string expectedContentPreRendered, string expectedContent)
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath,
                    TestSitePathConst05))
        };
        var parser = new YamlParser();
        var siteSettings =
            SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
        Site = new Site(options, siteSettings, parser, LoggerMock, null);

        // Act
        Site.ScanAndParseSourceFiles(_fs);
        Site.ProcessPages();

        // Assert
        _ = Site.OutputReferences.TryGetValue(
            new(url, UriKind.RelativeOrAbsolute), out var output);
        var page = output as IPage;
        Assert.NotNull(page);
        Assert.Equal(expectedContentPreRendered, page.ContentPreRendered);
        Assert.Equal(expectedContent, page.Content);
        Assert.Equal(expectedContent, page.CompleteContent);
    }

    /// <summary>Confirms that missing content yields empty content and complete content when no theme is configured.</summary>
    [Theory]
    [InlineData("/index.html")]
    [InlineData("/blog/index.html")]
    [InlineData("/tags/index.html")]
    [InlineData("/tags/tag1/index.html")]
    [InlineData("/blog/test-content-1/index.html")]
    public void PageContentShouldReturnThrowNullThemeBaseofContent(string url)
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath,
                    TestSitePathConst07))
        };
        var parser = new YamlParser();
        var siteSettings =
            SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
        Site = new Site(options, siteSettings, parser, LoggerMock, null);

        // Act
        Site.ScanAndParseSourceFiles(_fs);
        Site.ProcessPages();

        // Assert
        _ = Site.OutputReferences.TryGetValue(
            new(url, UriKind.RelativeOrAbsolute), out var output);
        var page = output as IPage;
        Assert.NotNull(page);
        Assert.Equal(string.Empty, page.Content);
        Assert.Equal(string.Empty, page.CompleteContent);
    }

    /// <summary>Confirms that pre-rendered, rendered, and complete content include the theme and baseof output.</summary>
    [Theory]
    [InlineData("/index.html",
        "<p>Index Content</p>\n",
        "INDEX-<p>Index Content</p>\n",
        "BASEOF-INDEX-<p>Index Content</p>\n")]
    [InlineData("/blog/index.html",
        "",
        "LIST-",
        "BASEOF-LIST-")]
    [InlineData("/tags/index.html",
        "",
        "LIST-",
        "BASEOF-LIST-")]
    [InlineData("/tags/tag1/index.html",
        "",
        "LIST-",
        "BASEOF-LIST-")]
    [InlineData("/blog/test-content-1/index.html",
        "<p>Test Content 1</p>\n",
        "SINGLE-<p>Test Content 1</p>\n",
        "BASEOF-SINGLE-<p>Test Content 1</p>\n")]
    public void PageContentShouldReturnThemeContent(string url,
        string expectedContentPreRendered, string expectedContent,
        string expectedOutputFile)
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath,
                    TestSitePathConst06))
        };
        var parser = new YamlParser();
        var siteSettings =
            SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
        Site = new Site(options, siteSettings, parser, LoggerMock, null);

        // Act
        Site.ScanAndParseSourceFiles(_fs);
        Site.ProcessPages();

        // Assert
        _ = Site.OutputReferences.TryGetValue(
            new(url, UriKind.RelativeOrAbsolute), out var output);
        var page = output as IPage;
        Assert.NotNull(page);
        Assert.Equal(expectedContentPreRendered, page.ContentPreRendered);
        Assert.Equal(expectedContent, page.Content);
        Assert.Equal(expectedOutputFile, page.CompleteContent);
    }

    /// <summary>Confirms that section pages and their nested pages are registered at the expected permalinks.</summary>
    [Fact]
    public void SiteShouldConsiderSectionPages()
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath,
                    TestSitePathConst09))
        };
        Site.Options = options;

        // Act
        Site.ScanAndParseSourceFiles(new FileSystem());
        Site.ProcessPages();

        // Assert
        Assert.Equal(12,
            Site.OutputReferences.Values.Count(output =>
                output is IPage { OutputFormat: "html" }));
        Assert.True(Site.OutputReferences.ContainsKey(
            new Uri("/pages/page-01/index.html", UriKind.RelativeOrAbsolute)));
        Assert.True(Site.OutputReferences.ContainsKey(
            new Uri("/blog/blog-01/index.html", UriKind.RelativeOrAbsolute)));
        Assert.True(Site.OutputReferences.ContainsKey(
            new Uri("/pages/page-01/page-01/index.html",
                UriKind.RelativeOrAbsolute)));
        Assert.True(Site.OutputReferences.ContainsKey(
            new Uri("/blog/blog-01/blog-01/index.html",
                UriKind.RelativeOrAbsolute)));
        Assert.True(Site.OutputReferences.ContainsKey(
            new Uri("/articles/article-01/index.html",
                UriKind.RelativeOrAbsolute)));
        Assert.True(Site.OutputReferences.ContainsKey(
            new Uri("/index/post-01/index.html", UriKind.RelativeOrAbsolute)));
        Assert.True(Site.OutputReferences.ContainsKey(
            new Uri("/index/post-01/post-01/index.html",
                UriKind.RelativeOrAbsolute)));
    }

    /// <summary>Confirms that page resources are published inside the page's own directory.</summary>
    [Fact]
    public void PageResourcesShouldBePublishedNextToTheirPage()
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath,
                    TestSitePathConst11))
        };
        Site.Options = options;

        // Act
        Site.ScanAndParseSourceFiles(_fs);
        Site.ProcessPages();

        // Assert
        // The resource must live inside the page's directory, not be flattened
        // into a sibling like "/my-postcover.webp".
        var resourcePermalink =
            new Uri("/my-post/cover.webp", UriKind.RelativeOrAbsolute);
        Assert.True(Site.OutputReferences.ContainsKey(resourcePermalink),
            "Expected resource to be registered at /my-post/cover.webp");
        Assert.IsAssignableFrom<IResource>(
            Site.OutputReferences[resourcePermalink]);
    }

    /// <summary>Confirms that resource definitions apply custom name, title, and params to published resources.</summary>
    [Fact]
    public void PageResourcesShouldApplyResourceDefinitionCustomization()
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath,
                    TestSitePathConst11))
        };
        Site.Options = options;

        // Act
        Site.ScanAndParseSourceFiles(_fs);
        Site.ProcessPages();

        // Assert
        // `name` renames the published file, `title` and `params` add metadata.
        var resourcePermalink =
            new Uri("/customized/renamed-cover.webp", UriKind.RelativeOrAbsolute);
        Assert.True(Site.OutputReferences.TryGetValue(resourcePermalink,
                out var output),
            "Expected renamed resource at /customized/renamed-cover.webp");

        var resource = Assert.IsAssignableFrom<IResource>(output);
        Assert.Equal("Cover art", resource.Title);
        Assert.Equal("Alt text", resource.Params["alt"]);
    }

    /// <summary>Confirms that the reported parsed file count equals the number of markdown source files.</summary>
    [Theory]
    [InlineData(TestSitePathConst01)]
    [InlineData(TestSitePathConst02)]
    [InlineData(TestSitePathConst03)]
    [InlineData(TestSitePathConst04)]
    [InlineData(TestSitePathConst09)]
    public void FilesParsedToReportShouldEqualMarkdownFileCount(string sitePath)
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath, sitePath))
        };
        var site = new Site(options, SiteSettingsMock, FrontMatterParser, LoggerMock, SystemClockMock);
        var markdownFiles = Directory.GetFiles(site.SourceContentPath, "*.md",
            SearchOption.AllDirectories);

        // Act
        site.ScanAndParseSourceFiles(_fs, site.SourceContentPath);
        site.ProcessPages();

        // Assert
        Assert.Equal(markdownFiles.Length, site.FilesParsedToReport);
    }

    /// <summary>Confirms that the reported created page count equals the total number of pages.</summary>
    [Fact]
    public void PagesCreatedToReportShouldCountEveryCreatedPage()
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath, TestSitePathConst01))
        };
        var site = new Site(options, SiteSettingsMock, FrontMatterParser, LoggerMock, SystemClockMock);

        // Act
        site.ScanAndParseSourceFiles(_fs, site.SourceContentPath);
        site.ProcessPages();

        // Assert
        Assert.Equal(site.Pages.Count(), site.PagesCreatedToReport);
    }

    /// <summary>Confirms that creating a child page also creates and links the pages of its parent content source.</summary>
    [Fact]
    public void PageCreateShouldAlsoCreateThePagesOfTheParent()
    {
        GenerateOptions options = new()
        {
            SourceArgument =
                Path.GetFullPath(Path.Combine(TestSitesPath, TestSitePathConst01))
        };
        var parser = new YamlParser();
        var siteSettings =
            SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
        var site = new Site(options, siteSettings, parser, LoggerMock, null);
        var parent = new ContentSource("blog/_index.md",
            new FrontMatter { Title = "Blog" }, string.Empty)
        {
            BundleType = BundleType.Branch
        };
        var child = new ContentSource("blog/post-01.md",
            new FrontMatter { Title = "Post" }, string.Empty)
        {
            ContentSourceParent = parent
        };

        // Act
        var pages = site.PageCreate(child);

        // Assert
        Assert.NotEmpty(parent.ContentSourceToPages);
        Assert.All(parent.ContentSourceToPages, page => Assert.Contains(page, pages));
        Assert.NotEmpty(child.ContentSourceToPages);
    }
}
