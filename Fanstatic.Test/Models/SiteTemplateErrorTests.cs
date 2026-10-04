using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.Parsers;
using Fanstatic.TemplateEngine;
using Serilog;
using Xunit;

namespace Fanstatic.Test.Models;

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

    [Fact]
    public void PreCompileTheme_ShouldReportSyntaxErrors_WithPositionAndExcerpt()
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

    [Fact]
    public void PreCompileTheme_ShouldReportNoErrors_ForValidTheme()
    {
        // Act
        var site = CreateSite(TestSitePathConst06);

        // Assert
        Assert.Empty(site.TemplateErrors);
    }

    [Fact]
    public void Page_Content_ShouldBeEmpty_WhenTemplateHasSyntaxError()
    {
        // Arrange
        var site = CreateSite(TestSitePathConst07);

        // Act
        var content = GetPage(site, "/index.html").CompleteContent;

        // Assert
        Assert.Equal(3, site.TemplateErrors.Count);
        Assert.Equal(string.Empty, content);
    }

    [Fact]
    public void Page_Content_ShouldReturnOverlay_WhenServingAndTemplateHasSyntaxError()
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

    [Fact]
    public void Page_Content_ShouldNameTemplateAndPage_WhenPartialIsMissing()
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

    [Fact]
    public void TemplateErrors_ShouldDeduplicateByTemplate()
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

    [Fact]
    public void TemplateErrors_ShouldReportHowManyTemplatesWereAdded()
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

    [Fact]
    public void TemplateErrors_ShouldTreatRelativeAndFullPathAsTheSameTemplate()
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

    [Fact]
    public void AddTemplateErrors_ShouldThrow_WhenErrorsAreNull()
    {
        // Arrange
        var site = CreateSite(TestSitePathConst06);

        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => site.AddTemplateErrors(null!));
    }

    [Fact]
    public void ResetCache_ShouldClearTemplateErrors()
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
