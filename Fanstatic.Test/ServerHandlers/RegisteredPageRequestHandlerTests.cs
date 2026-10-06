using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.Parsers;
using Fanstatic.ServerHandlers;
using NSubstitute;
using Xunit;

namespace Fanstatic.Test.ServerHandlers;

public class RegisteredPageRequestHandlerTests : TestSetup
{
    readonly IFileSystem _fs = new FileSystem();

    [Theory]
    [InlineData("/", true)]
    [InlineData("/testPage", false)]
    [InlineData("/index.html", true)]
    [InlineData("/testPage/index.html", false)]
    public void Check_ReturnsTrueForRegisteredPage(string requestPath, bool exist)
    {
        // Arrange
        var siteFullPath = Path.GetFullPath(Path.Combine(TestSitesPath, TestSitePathConst06));
        Site.Options = new GenerateOptions
        {
            SourceArgument = siteFullPath
        };
        var registeredPageRequest = new RegisteredPageRequest(Site);

        // Act
        Site.ScanAndParseSourceFiles(_fs, Path.Combine(siteFullPath, "content"));
        Site.ProcessPages();

        // Assert
        Assert.Equal(exist, registeredPageRequest.Check(new(requestPath, UriKind.RelativeOrAbsolute)));
    }

    [Fact]
    public async Task Check_RegistersCompactPaginatedTaxonomyUrl()
    {
        var siteFullPath = Path.GetFullPath(Path.Combine(TestSitesPath, ".TestSites/12-taxonomies"));
        var options = new GenerateOptions { SourceArgument = siteFullPath };
        var site = SiteHelper.Init(
            "fanstatic.yaml",
            options,
            new YamlParser(),
            LoggerMock,
            new StopwatchReporter(LoggerMock),
            _fs);
        var registeredPageRequest = new RegisteredPageRequest(site);

        var found = registeredPageRequest.Check(new Uri("/tags/release/2", UriKind.Relative));
        var foundCanonical = registeredPageRequest.Check(
            new Uri("/tags/release/page/2", UriKind.Relative));
        var foundFirstPageAlias = registeredPageRequest.Check(
            new Uri("/tags/release/1", UriKind.Relative));
        var foundInvalidPageAlias = registeredPageRequest.Check(
            new Uri("/tags/release/not-a-page", UriKind.Relative));
        var foundFirstCanonicalPage = registeredPageRequest.Check(
            new Uri("/tags/release/page/1", UriKind.Relative));

        Assert.True(found);
        Assert.True(foundCanonical);
        Assert.False(foundFirstPageAlias);
        Assert.False(foundInvalidPageAlias);
        Assert.False(foundFirstCanonicalPage);
        var paginatedPage = Assert.IsType<Page>(
            site.OutputReferences[new Uri("/tags/release/2/index.html", UriKind.Relative)]);
        Assert.Equal(2, paginatedPage.PageIndex);

        var response = Substitute.For<IHttpListenerResponse>();
        var stream = new MemoryStream();
        _ = response.OutputStream.Returns(stream);
        var result = await registeredPageRequest.Handle(
            response,
            new Uri("/tags/release/2", UriKind.Relative),
            DateTime.Now);

        Assert.Equal("dict", result);
        Assert.True(stream.Length > 0);
    }

    [Theory]
    [InlineData("/", TestSitePathConst06, false)]
    [InlineData("/", TestSitePathConst08, true)]
    [InlineData("/index.html", TestSitePathConst06, false)]
    [InlineData("/index.html", TestSitePathConst08, true)]
    public async Task Handle_ReturnsExpectedContent2(string requestPath, string testSitePath, bool contains)
    {
        // Arrange
        var siteFullPath = Path.GetFullPath(Path.Combine(TestSitesPath, testSitePath));
        GenerateOptions options = new()
        {
            SourceArgument = siteFullPath
        };
        var parser = new YamlParser();
        var siteSettings = SiteHelper.ParseSettings("fanstatic.yaml", options, parser, _fs);
        Site = new Site(options, siteSettings, parser, LoggerMock, null);

        var registeredPageRequest = new RegisteredPageRequest(Site);

        var response = Substitute.For<IHttpListenerResponse>();
        var stream = new MemoryStream();
        _ = response.OutputStream.Returns(stream);
        var requestUri = new Uri(requestPath, UriKind.RelativeOrAbsolute);

        // Act
        Site.ScanAndParseSourceFiles(_fs, Path.Combine(siteFullPath, "content"));
        Site.ProcessPages();
        _ = registeredPageRequest.Check(requestUri);
        var code = await registeredPageRequest.Handle(response, requestUri, DateTime.Now).ConfigureAwait(true);

        // Assert
        _ = stream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(CancellationToken.None).ConfigureAwait(true);

        Assert.Equal("dict", code);

        // Assert
        // You may want to adjust this assertion depending on the actual format of your injected script
        if (contains)
        {
            Assert.Contains("<script>", content, StringComparison.InvariantCulture);
            Assert.Contains("</script>", content, StringComparison.InvariantCulture);
        }
        else
        {
            Assert.DoesNotContain("</script>", content, StringComparison.InvariantCulture);
            Assert.DoesNotContain("</script>", content, StringComparison.InvariantCulture);
        }

        Assert.Contains("Index Content", content, StringComparison.InvariantCulture);
    }
}
