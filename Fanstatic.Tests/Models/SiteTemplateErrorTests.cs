using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.Parsers;
using Fanstatic.TemplateEngine;
using Serilog;
using Xunit;

namespace Fanstatic.Test.Models;

/// <summary>Tests for theme template compilation errors and their reporting.</summary>
public class SiteTemplateErrorTests : TestSetup
{
    readonly IFileSystem _fs = new FileSystem();

    readonly IFrontMatterParser _parser = new YamlParser();

    readonly StopwatchReporter _stopwatch =
        new(new LoggerConfiguration().CreateLogger());

    ISite CreateSite(string testSitePath)
    {
        var options = new GenerateOptions
        {
            SourceArgument = Path.GetFullPath(Path.Combine(TestSitesPath, testSitePath))
        };

        return SiteHelper.Init("fanstatic.yaml", options, _parser, LoggerMock, _stopwatch, _fs);
    }

    /// <summary>Confirms that theme syntax errors are reported with position and source excerpt.</summary>
    [Fact]
    public void PreCompileThemeShouldReportSyntaxErrorsWithPositionAndExcerpt()
    {
        // Act
        var site = CreateSite(TestSitePathConst07);

        // Assert
        Assert.Equal(3, site.TemplateErrors.Count);

        var indexError = Assert.Single(site.TemplateErrors,
            error => error.TemplatePath.EndsWith("_default/index.html", StringComparison.Ordinal));
        Assert.Equal("Unknown tag 'page'", indexError.Message);
        Assert.Equal(1, indexError.Line);
        Assert.Equal(14, indexError.Column);
        Assert.Equal("INDEX-{% page.ContentPreRendered %}", indexError.SourceExcerpt);
        Assert.Null(indexError.PageSourceRelativePath);
    }

    /// <summary>Confirms that a valid theme pre-compiles without template errors.</summary>
    [Fact]
    public void PreCompileThemeShouldReportNoErrorsForValidTheme()
    {
        // Act
        var site = CreateSite(TestSitePathConst06);

        // Assert
        Assert.Empty(site.TemplateErrors);
    }

    /// <summary>Confirms that page content is empty when its template has a syntax error.</summary>
    [Fact]
    public void PageContentShouldBeEmptyWhenTemplateHasSyntaxError()
    {
        // Arrange
        var site = CreateSite(TestSitePathConst07);

        // Act
        var content = GetPage(site, "/index.html").CompleteContent;

        // Assert
        Assert.Equal(3, site.TemplateErrors.Count);
        Assert.Equal(string.Empty, content);
    }

    /// <summary>Confirms that a template error overlay is rendered when serving.</summary>
    [Fact]
    public void PageContentShouldReturnOverlayWhenServingAndTemplateHasSyntaxError()
    {
        // Arrange
        var site = CreateSite(TestSitePathConst07);
        site.Fanstatic.IsServer = true;

        // Act
        var content = GetPage(site, "/index.html").CompleteContent;

        // Assert
        Assert.Contains("Template error", content, StringComparison.Ordinal);
        Assert.Contains("_default/index.html:1:14", content, StringComparison.Ordinal);
    }

    /// <summary>Confirms that a missing partial error names the template and page.</summary>
    [Fact]
    public void PageContentShouldNameTemplateAndPageWhenPartialIsMissing()
    {
        // Arrange
        var site = CreateSite(TestSitePathConst15);

        // Act
        var content = GetPage(site, "/blog/post-one/index.html").CompleteContent;

        // Assert
        Assert.Equal("BASEOF-", content);

        var error = Assert.Single(site.TemplateErrors);
        Assert.EndsWith("_default/single.html", error.TemplatePath, StringComparison.Ordinal);
        Assert.Equal("template not found: 'partials/missing.html'", error.Message);
        Assert.Equal(1, error.Line);
        Assert.Equal(8, error.Column);
        Assert.Equal("SINGLE-{% include 'partials/missing.html' %}", error.SourceExcerpt);
        Assert.Equal("blog/post-1.md", error.PageSourceRelativePath);
    }

    /// <summary>Confirms that template errors are deduplicated per template.</summary>
    [Fact]
    public void TemplateErrorsShouldDeduplicateByTemplate()
    {
        // Arrange
        var site = CreateSite(TestSitePathConst06);
        TemplateError[] errors =
        [
            new("/themes/test/_default/single.html", "First", 1, 1, null, "a.md"),
            new("/themes/test/_default/single.html", "Second", 2, 2, null, "b.md")
        ];

        // Act
        var added = site.AddTemplateErrors(errors);

        // Assert
        Assert.Equal(1, added);
        var error = Assert.Single(site.TemplateErrors);
        Assert.Equal("First", error.Message);
    }

    /// <summary>Confirms that adding errors reports how many distinct templates were added.</summary>
    [Fact]
    public void TemplateErrorsShouldReportHowManyTemplatesWereAdded()
    {
        // Arrange
        var site = CreateSite(TestSitePathConst06);
        TemplateError[] errors =
        [
            new("/themes/test/_default/single.html", "First", 1, 1, null, "a.md"),
            new("/themes/test/_default/list.html", "Second", 2, 2, null, "b.md")
        ];

        // Act
        var added = site.AddTemplateErrors(errors);

        // Assert
        Assert.Equal(2, added);
        Assert.Equal(2, site.TemplateErrors.Count);
    }

    /// <summary>Confirms that relative and full template paths resolve to the same template.</summary>
    [Fact]
    public void TemplateErrorsShouldTreatRelativeAndFullPathAsTheSameTemplate()
    {
        // Arrange
        var site = CreateSite(TestSitePathConst06);
        var fullPath = Path.GetFullPath("themes/test/_default/single.html");
        TemplateError[] errors =
        [
            new(fullPath, "First", 1, 1, null, null),
            new("themes/test/_default/single.html", "Second", 2, 2, null, "b.md")
        ];

        // Act
        var added = site.AddTemplateErrors(errors);

        // Assert
        Assert.Equal(1, added);
        Assert.Equal("First", Assert.Single(site.TemplateErrors).Message);
    }

    /// <summary>Confirms that adding a null error collection throws.</summary>
    [Fact]
    public void AddTemplateErrorsShouldThrowWhenErrorsAreNull()
    {
        // Arrange
        var site = CreateSite(TestSitePathConst06);

        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => site.AddTemplateErrors(null!));
    }

    /// <summary>Confirms that resetting the cache clears collected template errors.</summary>
    [Fact]
    public void ResetCacheShouldClearTemplateErrors()
    {
        // Arrange
        var site = CreateSite(TestSitePathConst07);
        Assert.NotEmpty(site.TemplateErrors);

        // Act
        site.ResetCache();

        // Assert
        Assert.Empty(site.TemplateErrors);
    }

    static IPage GetPage(ISite site, string url)
    {
        Assert.True(site.OutputReferences.TryGetValue(new(url, UriKind.RelativeOrAbsolute), out var output),
            string.Join(" | ", site.OutputReferences.Keys.Select(key => key.OriginalString)));
        return Assert.IsAssignableFrom<IPage>(output);
    }
}
