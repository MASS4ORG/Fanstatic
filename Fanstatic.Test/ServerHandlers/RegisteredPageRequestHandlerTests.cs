using NSubstitute;
using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.Parsers;
using Fanstatic.ServerHandlers;
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
