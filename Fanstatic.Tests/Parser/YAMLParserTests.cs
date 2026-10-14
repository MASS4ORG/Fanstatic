using System.Collections.Concurrent;
using System.Globalization;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.Parsers;
using Xunit;

namespace Fanstatic.Test.Parser;

/// <summary>Tests for YAML front matter and site settings parsing.</summary>
public class YamlParserTests : TestSetup
{
    readonly YamlParser _parser = new();

    [Fact]
    /// <summary>Confirms that a scalar where a list is expected throws a concise format exception.</summary>
    public void ParseWithScalarWhereListExpectedShouldThrowConciseFormatException()
    {
        var exception = Assert.Throws<FormatException>(() => _parser.Parse<FrontMatter>("aliases: single"));

        Assert.Contains("front matter line 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("single value where a list is expected", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    /// <summary>Confirms that a shared parser handles concurrent parse calls.</summary>
    public void ParseShouldSupportConcurrentCallsOnSharedParser()
    {
        var parsedTitles = new ConcurrentBag<string>();

        Parallel.For(0, 1_000, index =>
        {
            var result = _parser.Parse<FrontMatter>($"title: title-{index}");
            parsedTitles.Add(result.Title
                ?? throw new InvalidOperationException("Concurrent YAML parsing returned a null title."));
        });

        Assert.Equal(1_000, parsedTitles.Count);
        Assert.Equal(1_000, parsedTitles.Distinct(StringComparer.Ordinal).Count());
    }

    // TODO: consider using these
    // Date: 2023-07-01
    // LastMod: 2023-06-01
    // PublishDate: 2023-06-01
    const string PageFrontMatterConst = """
                                                Title: Test Title
                                                Type: post
                                                Date: 2023-01-01
                                                LastMod: 2023-02-01
                                                PublishDate: 2023-03-01
                                                ExpiryDate: 2024-06-01
                                                Tags:
                                                  - Test
                                                  - Real Data
                                                Categories:
                                                  - Test
                                                  - Real Data
                                                NestedData:
                                                  Level2:
                                                    - Test
                                                    - Real Data
                                                customParam: Custom Value
                                                Params:
                                                  ParamsCustomParam: Custom Value
                                                  ParamsNestedData:
                                                    Level2:
                                                      - Test
                                                      - Real Data

                                                """;

    const string PageMarkdownConst = """
                                             # Real Data Test

                                             This is a test using real data. Real Data Test

                                             """;

    const string SiteContentConst = """
                                            Title: My Site
                                            BaseURL: https://www.example.com/
                                            Description: For Fans of a Fantastic Static Site Generator
                                            Copyright: Copyright message
                                            customParam: Custom Value
                                            NestedData:
                                              Level2:
                                                - Test
                                                - Real Data
                                            Params:
                                              ParamsCustomParam: Custom Value
                                              ParamsNestedData:
                                                Level2:
                                                  - Test
                                                  - Real Data

                                            """;

    const string FileFullPathConst = "test.md";
    const string FileRelativePathConst = "test.md";

    const string PageContent = $"""
                                        ---
                                        {PageFrontMatterConst}
                                        ---
                                        {PageMarkdownConst}
                                        """;

    static readonly string[] Expected = ["Test", "Real Data"];

    [Fact]
    /// <summary>Confirms that the section is the first folder in the path.</summary>
    public void GetSectionShouldReturnFirstFolderName()
    {
        // Arrange
        var filePath = Path.Combine("folder1", "folder2", "file.md");

        // Act
        var section = SiteHelper.GetSection(filePath);

        // Assert
        Assert.Equal("folder1", section);
    }

    [Theory]
    [InlineData("""
                ---
                Title: Test Title
                ---

                """, "Test Title")]
    [InlineData("""
                ---
                Date: 2023-04-01
                ---

                """, "")]
    /// <summary>Confirms that the front matter title is parsed.</summary>
    public void ParseFrontMatterShouldParseTitleCorrectly(string fileContent,
        string expectedTitle)
    {
        // Arrange
        var (frontMatter, _) = FrontMatter.Parse(FileRelativePathConst, FileFullPathConst, _parser, fileContent);

        // Assert
        Assert.Equal(expectedTitle, frontMatter.Title);
    }

    [Theory]
    [InlineData("""
                ---
                Date: 2023-01-01
                ---

                """, "2023-01-01")]
    [InlineData("""
                ---
                Date: 2023/01/01
                ---

                """, "2023-01-01")]
    /// <summary>Confirms that the front matter date is parsed.</summary>
    public void ParseFrontMatterShouldParseDateCorrectly(string fileContent,
        string expectedDateString)
    {
        // Arrange
        var expectedDate = DateTime.Parse(expectedDateString,
            CultureInfo.InvariantCulture);

        // Act
        var (frontMatter, _) = FrontMatter.Parse(FileRelativePathConst,
            FileFullPathConst, _parser, fileContent);

        // Assert
        Assert.Equal(expectedDate, frontMatter.Date);
    }

    [Fact]
    /// <summary>Confirms that the remaining front matter date fields are parsed.</summary>
    public void ParseFrontMatterShouldParseOtherFieldsCorrectly()
    {
        // Arrange
        var expectedDate = DateTime.Parse("2023-01-01", CultureInfo.InvariantCulture);
        var expectedLastMod = DateTime.Parse("2023-02-01", CultureInfo.InvariantCulture);
        var expectedPublishDate = DateTime.Parse("2023-03-01", CultureInfo.InvariantCulture);
        var expectedExpiryDate = DateTime.Parse("2024-06-01", CultureInfo.InvariantCulture);

        // Act
        var (frontMatter, _) = FrontMatter.Parse(FileRelativePathConst, FileFullPathConst, _parser, PageContent);

        // Assert
        Assert.Equal("Test Title", frontMatter.Title);
        Assert.Equal("post", frontMatter.Type);
        Assert.Equal(expectedDate, frontMatter.Date);
        Assert.Equal(expectedLastMod, frontMatter.LastMod);
        Assert.Equal(expectedPublishDate, frontMatter.PublishDate);
        Assert.Equal(expectedExpiryDate, frontMatter.ExpiryDate);
    }

    [Fact]
    /// <summary>Confirms that invalid YAML front matter throws a format exception.</summary>
    public void
        ParseFrontMatterShouldThrowFormatExceptionWhenInvalidYAMLSyntax()
    {
        // Arrange
        const string fileContent = """
                                   ---
                                   Title
                                   ---

                                   """;

        // Assert
        Assert.Throws<FormatException>(() =>
            FrontMatter.Parse(FileRelativePathConst, FileFullPathConst, _parser, fileContent));
    }

    [Fact]
    /// <summary>Confirms that site settings are parsed into a site configuration.</summary>
    public void ParseSiteSettingsShouldReturnSiteWithCorrectSettings()
    {
        // Act
        var siteSettings = _parser.Parse<SiteSettings>(SiteContentConst);

        // Assert
        Assert.Equal("My Site", siteSettings.Title);
        Assert.Equal(new("https://www.example.com/"), siteSettings.BaseUrl);
        Assert.Equal("For Fans of a Fantastic Static Site Generator", siteSettings.Description);
        Assert.Equal("Copyright message", siteSettings.Copyright);
    }

    [Fact]
    /// <summary>Confirms that non-matching front matter fields populate params.</summary>
    public void ParseParamsShouldFillParamsWithNonMatchingFields()
    {
        // Arrange
        var (frontMatter, _) = FrontMatter.Parse(string.Empty, string.Empty,
            _parser, PageContent);
        ContentSource contentSource = new(string.Empty, frontMatter, string.Empty);
        var page = new Page(contentSource, Site, Site, ("html", null), []);

        // Assert
        Assert.False(page.Params.ContainsKey("customParam"));
        Assert.Equal("Custom Value", page.Params["ParamsCustomParam"]);
    }

    [Fact]
    /// <summary>Confirms that a page in the site folder is post-processed with its date.</summary>
    public void ParseFrontMatterShouldParseContentInSiteFolder()
    {
        // Arrange
        var date = DateTime.Parse("2023-01-01", CultureInfo.InvariantCulture);
        var (frontMatter, _) = FrontMatter.Parse(string.Empty, string.Empty, _parser, PageContent);
        ContentSource contentSource = new(string.Empty, frontMatter, string.Empty);
        var page = new Page(contentSource, Site, Site, ("html", null), []);

        // Act
        Site.PostProcessPage(page);

        // Assert
        Assert.Equal(date, frontMatter.Date);
    }

    [Fact]
    /// <summary>Confirms that front matter tags are created as taxonomy references.</summary>
    public void ParseFrontMatterShouldCreateTags()
    {
        // Arrange
        var (frontMatter, _) = FrontMatter.Parse(string.Empty, string.Empty, _parser, PageContent);
        ContentSource contentSource =
            new(string.Empty, frontMatter, string.Empty)
            {
                Kind = Kind.single
            };

        // Act
        Site.ContentSourceAdd(contentSource);
        Site.ProcessPages();
        Site.OutputReferences.TryGetValue(new("/test-title/index.html", UriKind.RelativeOrAbsolute), out var output);
        var page = output as Page;

        // Assert
        Assert.Equal(2, page?.TagsReference.Count(cs => cs.OutputFormat == "html"));
        Assert.Equal(2, page?.TagsReference.Count);
    }

    [Fact]
    /// <summary>Confirms that front matter parsing throws when raw content is missing.</summary>
    public void FrontMatterParseRawContentNull()
    {
        Assert.Throws<FormatException>(() =>
            FrontMatter.Parse("""
                              ---
                              Title: Page 1
                              ---
                              """, "fakePath", "fakePath",
                FrontMatterParser));
    }

    [Fact]
    /// <summary>Confirms that parsing invalid front matter throws a format exception.</summary>
    public void ParseYAMLShouldThrowExceptionWhenFrontMatterIsInvalid()
    {
        Assert.Throws<FormatException>(() => _parser.Parse<FrontMatter>("invalidFrontMatter"));
    }

    [Fact]
    /// <summary>Confirms that front matter and content are split apart correctly.</summary>
    public void ParseYAMLShouldSplitTheFrontMatter()
    {
        // Act
        var (frontMatter, rawContent) = _parser.SplitFrontMatterAndContent(PageContent);
        frontMatter = frontMatter.Replace("\r\n", "\n", StringComparison.Ordinal);
        var pageFrontMatterConst = PageFrontMatterConst.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

        // Assert
        Assert.Equal(pageFrontMatterConst, frontMatter);
        Assert.Equal(PageMarkdownConst, rawContent);
    }

    [Fact]
    /// <summary>Confirms that site settings are parsed from content.</summary>
    public void ParseSiteSettingsShouldReturnSiteSettings()
    {
        // Arrange
        var siteSettings = _parser.Parse<SiteSettings>(SiteContentConst);

        // Assert
        Assert.NotNull(siteSettings);
        Assert.Equal("My Site", siteSettings.Title);
        Assert.Equal(new("https://www.example.com/"), siteSettings.BaseUrl);
    }

    [Fact]
    /// <summary>Confirms that taxonomies default and can be disabled.</summary>
    public void ParseSiteSettingsShouldDefaultAndAllowDisablingTaxonomies()
    {
        var defaults = _parser.Parse<SiteSettings>("Title: Test");
        var disabled = _parser.Parse<SiteSettings>("Title: Test\nTaxonomies: {}");

        Assert.Equal("tags", defaults.Taxonomies["tag"]);
        Assert.Equal("categories", defaults.Taxonomies["category"]);
        Assert.Empty(disabled.Taxonomies);
    }


    [Fact]
    /// <summary>Confirms that a site with empty content has no params.</summary>
    public void SiteParamsShouldHandleEmptyContent()
    {
        Assert.Empty(Site.Params);
    }

    [Fact]
    /// <summary>Confirms that extra site fields populate params.</summary>
    public void SiteParamsShouldPopulateParamsWithExtraFields()
    {
        // Arrange
        var siteSettings = _parser.Parse<SiteSettings>(SiteContentConst);
        Site = new Site(GenerateOptionsMock, siteSettings, FrontMatterParser,
            LoggerMock, SystemClockMock);

        // Assert
        Assert.NotEmpty(siteSettings.Params);
        Assert.DoesNotContain("customParam", Site.Params);
        Assert.Contains("ParamsCustomParam", Site.Params);
        Assert.Equal("Custom Value", Site.Params["ParamsCustomParam"]);
        Assert.Equal(Expected, ((Dictionary<string, object>)siteSettings.Params["ParamsNestedData"])["Level2"]);
        Assert.Equal("Test",
            ((siteSettings.Params["ParamsNestedData"] as Dictionary<string, object>)?["Level2"] as List<object>)?[0]);
    }

    [Theory]
    [InlineData("""
                ---
                Title: title-test
                Url: my-page
                ---
                """)]
    [InlineData("""
                ---
                title: title-test
                url: my-page
                ---
                """)]
    [InlineData("""
                ---
                tiTle: title-test
                URL: my-page
                ---
                """)]
    [InlineData("""
                ---
                tiTle: title-test-old
                title: title-test       # the last on is used
                Url: my-page
                url: my-page
                ---
                """)]
    /// <summary>Confirms that front matter keys are matched case-insensitively.</summary>
    public void FrontMatterShouldIgnoreCase(string fileContent)
    {
        // Arrange
        var (frontMatter, _) = FrontMatter.Parse(FileRelativePathConst, FileFullPathConst, _parser, fileContent);

        // Assert
        Assert.Equal("title-test", frontMatter.Title);
        Assert.Equal("my-page", frontMatter.Url);
    }

    [Theory]
    [InlineData("""
                Title: title-test
                BaseURL: https://www.example.com/
                """)]
    [InlineData("""
                title: title-test
                baseurl: https://www.example.com/
                """)]
    [InlineData("""
                tiTle: title-test
                baseUrl: https://www.example.com/
                """)]
    [InlineData("""
                tiTle: title-test-old
                Title: title-test                   # the last on is used
                baseurl: https://www.example2.com/
                BaseURL: https://www.example.com/   # the last on is used
                """)]
    /// <summary>Confirms that site setting keys are matched case-insensitively.</summary>
    public void SiteSettingsShouldIgnoreCase(string fileContent)
    {
        // Arrange
        var siteSettings = _parser.Parse<SiteSettings>(fileContent);
        Site = new Site(GenerateOptionsMock, siteSettings, FrontMatterParser, LoggerMock, SystemClockMock);

        // Assert
        Assert.Equal("title-test", Site.Title);
        Assert.Equal(new("https://www.example.com/"), (Site as ISiteOutput).BaseUrl);
    }
}
