using System.Net;
using NSubstitute;
using Fanstatic.Commands.ValidateLinks;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Xunit;

namespace Fanstatic.Test.Commands;

public class ValidateLinksCommandTests : TestSetup
{
    readonly IFileSystem _fileSystemMock;
    readonly IHttpClientWrapper _httpClientMock;
    readonly ValidateLinksOptions _validateLinksOptionsMock;
    readonly HttpResponseMessage _okResponse;

    public ValidateLinksCommandTests()
    {
        _httpClientMock = Substitute.For<IHttpClientWrapper>();
        _validateLinksOptionsMock = new ValidateLinksOptions
        {
            CheckExternal = true,
            Ignore = ["/ignored-link"]
        };
        _okResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                                        <html>
                                            <body>
                                                <h2 id="external-fragment">External Fragment</h2>
                                            </body>
                                        </html>
                                        """)
        };

        // Setup default file system behavior
        _fileSystemMock = Substitute.For<IFileSystem>();
        _fileSystemMock.FileExists("./fanstatic.yaml").Returns(true);
        _fileSystemMock.FileReadAllText("./fanstatic.yaml").Returns("""
                                                                Title: test
                                                                BaseUrl: https://example.com
                                                                """);

        SiteSettings iteSettingsMock = new()
        {
            Title = "test",
            BaseUrl = new("http://example.com")
        };
        Site = new Site(GenerateOptionsMock, iteSettingsMock, FrontMatterParser, LoggerMock, SystemClockMock);
        var testPage = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = TitleConst,
        }, """
               content with [fragment link](#fragment-link)

               ## Fragment Link

               """),
            Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Constructor_ShouldSetCheckExternalFlag(bool checkExternal, bool useSite)
    {
        // Arrange
        var options = new ValidateLinksOptions { CheckExternal = checkExternal };
        var site = useSite ? Site : null;

        // Act
        var validator = new ValidateLinksCommand(options, LoggerMock, _fileSystemMock, _httpClientMock, site);

        // Assert
        Assert.NotNull(validator);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenOptionsIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ValidateLinksCommand(null!, LoggerMock, _fileSystemMock, _httpClientMock));
    }

    [Theory]
    [InlineData("/test-title", 0)]
    [InlineData("/test-title/index.html", 0)]
    [InlineData("http://example.com/test-title", 0)]
    [InlineData("http://example.com/test-title/index.html", 0)]
    [InlineData("/invalid-internal", 1)]
    [InlineData("/invalid-internal/index.html", 1)]
    public async Task ValidateInternalLink_ShouldHandleValidAndInvalidLinks(string link, int fails)
    {
        var testPage2 = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = "Test Title 2",
        }, $"content with [link]({link})"), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage2);
        var validator = new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock,
            _httpClientMock, Site);

        // Act
        await validator.Parse();

        // Assert
        Assert.Equal(fails, validator.PagesWithFailedLinks.Count);
    }

    [Theory]
    [InlineData("/test-title#fragment-link", 0)]
    [InlineData("/test-title/index.html#fragment-link", 0)]
    [InlineData("/test-title#fragment-link-2", 1)]
    [InlineData("/test-title/index.html#fragment-link-2", 1)]
    public async Task ValidateInternalLink_ShouldHandleValidAndInvalidLinksWithFragments(string link, int fails)
    {
        var testPage2 = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = "Test Title 2",
        }, $"content with [link]({link})"), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage2);
        var validator = new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock,
            _httpClientMock, Site);

        // Act
        await validator.Parse();

        // Assert
        Assert.Equal(fails, validator.PagesWithFailedLinks.Count);
    }

    [Theory]
    [InlineData("mailto:test@example.com")]
    [InlineData("tel:+1234567890")]
    [InlineData("javascript:void(0)")]
    public async Task ValidateSpecialLinks_ShouldBeIgnored(string link)
    {
        // Arrange
        var testPage2 = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = TitleConst,
        },
            $"content with [link]({link})"), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage2);
        var validator = new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock,
            _httpClientMock, Site);

        // Act
        await validator.Parse();

        // Assert
        Assert.Empty(validator.PagesWithFailedLinks);
    }

    [Fact]
    public async Task Parse_ShouldPopulatePagesWithFailedLinks()
    {
        var testPage2 = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = "Test Page 2",
        },
            $"content with [link](/invalid-link)"), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage2);
        var uri = new Uri("/test-page-2/index.html", UriKind.RelativeOrAbsolute);

        // Act
        var validator = new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock,
            _httpClientMock, Site);
        await validator.Parse();

        Assert.True(validator.PagesWithFailedLinks.Any());
        Assert.True(Site.OutputReferences.ContainsKey(uri));
        Assert.Contains(validator.PagesWithFailedLinks, output => output.Key is Page page && page.RelPermalink == uri);
    }

    [Fact]
    public async Task GenerateReport_ShouldReturnCorrectExitCode()
    {
        var validator = new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock,
            _httpClientMock, Site);

        await validator.Parse();
        var result = await validator.GenerateReport();

        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData("/ignored-link")]
    [InlineData("/another-ignored")]
    public async Task Run_ShouldIgnoreSpecifiedLinks(string link)
    {
        // Arrange
        var testPage2 = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = TitleConst,
        },
            $"content with [link]({link})"), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage2);
        var options = new ValidateLinksOptions
        {
            CheckExternal = true,
            Ignore = [link]
        };
        var validator = new ValidateLinksCommand(options, LoggerMock, _fileSystemMock, _httpClientMock, Site);

        // Act
        await validator.Parse();

        // Assert
        Assert.Empty(validator.PagesWithFailedLinks);
    }


    [Theory]
    [InlineData("https://external.com", HttpStatusCode.OK, ValidateLinksCommand.LinkStatus.ok)]
    [InlineData("https://external.com", HttpStatusCode.NotFound, ValidateLinksCommand.LinkStatus.notFound)]
    [InlineData("https://external.com", HttpStatusCode.InternalServerError, ValidateLinksCommand.LinkStatus.httpError)]
    public async Task ValidateExternalLink_ShouldHandleHttpStatusCodes(string url, HttpStatusCode statusCode,
        ValidateLinksCommand.LinkStatus expectedStatus)
    {
        using var response = new HttpResponseMessage(statusCode);
        response.Content = new StringContent("");
        _httpClientMock.GetAsync(Arg.Any<Uri>()).Returns(response);

        var validator =
            new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock, _httpClientMock, Site)
            {
                DefaultTimeout = 10,
                DefaultRetryInterval = 0,
            };
        var result = await validator.ValidateExternalLink(new Uri(url), _httpClientMock);

        Assert.Equal(expectedStatus, result);
    }

    [Theory]
    [InlineData("https://external.com#external-fragment", ValidateLinksCommand.LinkStatus.ok)]
    [InlineData("https://external.com#non-existent", ValidateLinksCommand.LinkStatus.fragmentNotFound)]
    public async Task ValidateExternalLink_ShouldHandleFragments(string url,
        ValidateLinksCommand.LinkStatus expectedStatus)
    {
        _httpClientMock.GetAsync(Arg.Any<Uri>()).Returns(_okResponse);

        var validator =
            new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock, _httpClientMock, Site)
            {
                DefaultTimeout = 10,
                DefaultRetryInterval = 0,
            };
        var result = await validator.ValidateExternalLink(new Uri(url), _httpClientMock);

        Assert.Equal(expectedStatus, result);
    }

    [Theory]
    [InlineData("#fragment-link", 1)]
    [InlineData("#non-existent", 1)]
    [InlineData("#Fragment-Link", 1)]
    [InlineData("#fragment-link-2", 1)]
    [InlineData("/test-title#fragment-link", 0)]
    [InlineData("/test-title#non-existent", 1)]
    [InlineData("/test-title#Fragment-Link", 1)]
    [InlineData("/test-title#fragment-link-2", 1)]
    public async Task ValidateInternalLink_ShouldHandleFragmentCaseSensitivity(string fragment, int fails)
    {
        var testPage2 = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = "Test Title 2",
        },
            $"content with [link]({fragment})"), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage2);
        var validator = new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock,
            _httpClientMock, Site);

        await validator.Parse();

        Assert.Equal(fails, validator.PagesWithFailedLinks.Count);
    }

    [Theory]
    [InlineData("https://external.com/#/#", ValidateLinksCommand.LinkStatus.ok)]
    [InlineData("https://external.com/#/test", ValidateLinksCommand.LinkStatus.ok)]
    public async Task ValidateExternalLink_ShouldHandleSpecialFragments(string url,
        ValidateLinksCommand.LinkStatus expectedStatus)
    {
        _httpClientMock.GetAsync(Arg.Any<Uri>()).Returns(_okResponse);

        var validator =
            new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock, _httpClientMock, Site)
            {
                DefaultTimeout = 10,
                DefaultRetryInterval = 0,
            };
        var result = await validator.ValidateExternalLink(new Uri(url), _httpClientMock);

        Assert.Equal(expectedStatus, result);
    }

    [Theory]
    [InlineData(true, 1)] // Has failed links
    [InlineData(false, 0)] // No failed links
    public async Task GenerateReport_ShouldReturnCorrectExitCodeAndLogMessages(bool hasFailedLinks,
        int expectedExitCode)
    {
        var validator = new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock,
            _httpClientMock, Site);

        if (hasFailedLinks)
        {
            var testPage = new Page(new(SourcePathConst, new FrontMatter { Title = TitleConst }, "content"), Site, Site,
                ("html", null), []);
            validator.PagesWithFailedLinks.TryAdd(testPage,
                [("invalid-link", ValidateLinksCommand.LinkStatus.notFound)]);
        }

        var result = await validator.GenerateReport();
        Assert.Equal(expectedExitCode, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateExternalLink_ShouldHandleOperationCanceledException(bool shouldTimeout)
    {
        _httpClientMock
            .GetAsync(Arg.Any<Uri>())
            .Returns(Task.FromException<HttpResponseMessage>(new OperationCanceledException()));

        var validator =
            new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock, _httpClientMock, Site)
            {
                DefaultRetryCount = shouldTimeout ? 1 : 3,
                DefaultRetryInterval = 1,
                DefaultTimeout = 1
            };

        var result = await validator.ValidateExternalLink(new Uri("https://external.com"), _httpClientMock);
        Assert.Equal(ValidateLinksCommand.LinkStatus.timeout, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateExternalLink_ShouldHandleGenericException(bool shouldFail)
    {
        _httpClientMock
            .GetAsync(Arg.Any<Uri>())
#pragma warning disable CA2201
            .Returns(Task.FromException<HttpResponseMessage>(new Exception("Generic error")));
#pragma warning restore CA2201

        var validator =
            new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock, _httpClientMock, Site)
            {
                DefaultRetryCount = shouldFail ? 1 : 3,
                DefaultRetryInterval = 1
            };

        var result =
            await validator.ValidateExternalLink(new Uri("https://external.com"), _httpClientMock);
        Assert.Equal(ValidateLinksCommand.LinkStatus.httpError, result);
    }

    [Theory]
    [InlineData(true, 1)] // CheckExternal=true, should validate and fail
    [InlineData(false, 0)] // CheckExternal=false, should skip validation
    public async Task ValidateLinks_ShouldRespectCheckExternalFlag(bool checkExternal, int expectedFailures)
    {
        var options = new ValidateLinksOptions { CheckExternal = checkExternal };
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        _httpClientMock.GetAsync(Arg.Any<Uri>())
            .Returns(Task.FromResult(response));

        var testPage = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = "External Links",
        },
            "content with [link](https://external.com)"), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage);

        var validator = new ValidateLinksCommand(options, LoggerMock, _fileSystemMock, _httpClientMock, Site);
        await validator.Parse();

        Assert.Equal(expectedFailures, validator.PagesWithFailedLinks.Count);
    }

    [Fact]
    public async Task ValidatePageLinks_ShouldAccumulateFailedLinks()
    {
        var testPage = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = "Test Page"
        }, """
               [link1](/invalid1)
               [link2](/invalid2)
               """)
        {
            Kind = Kind.single
        },
            Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage);

        var validator = new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock,
            _httpClientMock, Site);
        await validator.Parse();

        Assert.True(validator.PagesWithFailedLinks.TryGetValue(testPage, out var failedLinks));
        Assert.Equal(2, failedLinks.Count);
    }

    [Theory]
    [InlineData("/ignored-link", true)]
    [InlineData("/not-ignored", false)]
    [InlineData("https://external.com/ignored-link", true)]
    [InlineData("https://external.com/not-ignored", false)]
    public async Task ValidatePageLinks_ShouldRespectIgnoreSettings(string link, bool shouldBeIgnored)
    {
        var options = new ValidateLinksOptions
        {
            CheckExternal = true,
            Ignore = ["/ignored-link", "https://external.com/ignored-link"]
        };

        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        _httpClientMock.GetAsync(Arg.Any<Uri>())
            .Returns(Task.FromResult(response));

        var testPage = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = "Test Page",
        },
            $"content with [link]({link})"), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage);

        var validator = new ValidateLinksCommand(options, LoggerMock, _fileSystemMock, _httpClientMock, Site);
        await validator.Parse();

        Assert.Equal(!shouldBeIgnored, validator.PagesWithFailedLinks.Any());
    }

    [Fact]
    public async Task ValidatePageLinks_ShouldIgnoreMultipleLinks()
    {
        var options = new ValidateLinksOptions
        {
            CheckExternal = true,
            Ignore = ["/ignored1", "/ignored2", "https://external.com/ignored3"]
        };

        var testPage = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = "Test Page",
        }, """
           [link1](/ignored1)
           [link2](/ignored2)
           [link3](https://external.com/ignored3)
           """), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage);

        var validator = new ValidateLinksCommand(options, LoggerMock, _fileSystemMock, _httpClientMock, Site);
        await validator.Parse();

        Assert.Empty(validator.PagesWithFailedLinks);
    }

    [Fact]
    public async Task ValidatePageLinks_ShouldIgnoreWildcardPatterns()
    {
        var options = new ValidateLinksOptions
        {
            CheckExternal = true,
            Ignore = ["https://external.com/*", "/docs/*"]
        };

        var testPage = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = "Test Page",
        }, """
           [link1](https://external.com/any/path)
           [link2](/docs/any/path)
           """), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage);

        var validator = new ValidateLinksCommand(options, LoggerMock, _fileSystemMock, _httpClientMock, Site)
        {
            DefaultTimeout = 10,
            DefaultRetryInterval = 0,
        };
        await validator.Parse();

        Assert.Empty(validator.PagesWithFailedLinks);
    }

    [Fact]
    public async Task ValidateExternalLink_ShouldNotBeMisclassifiedAsInternalWhenSiteHasHomepage()
    {
        // Regression test: a root-path external link (e.g. a hash-routed SPA like
        // https://matrix.to/#/!room:matrix.org, whose real path is just "/") must not be
        // resolved against the site's own homepage just because nearly every site has a page
        // at "/index.html" too. Otherwise the "#/" special-casing in external fragment
        // validation never runs and the link is wrongly reported as fragmentNotFound.
        var homePage = new Page(new(SourcePathConst, new FrontMatter { Title = "Home" }, "home content"),
            Site, Site, ("html", null), [])
        {
            RelPermalink = new Uri("/index.html", UriKind.RelativeOrAbsolute)
        };
        Site.OutputReferences.TryAdd(homePage.RelPermalink, homePage);

        _httpClientMock.GetAsync(Arg.Any<Uri>()).Returns(_okResponse);

        var testPage2 = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = "Test Title 2",
        }, "content with [link](https://matrix.to/#/!vRaFlDqBZyMXNRKDch:matrix.org)"), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage2);

        var validator = new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock,
            _httpClientMock, Site);

        await validator.Parse();

        Assert.Empty(validator.PagesWithFailedLinks);
        await _httpClientMock.Received().GetAsync(Arg.Is<Uri>(u => u.Host == "matrix.to"));
    }

    [Fact]
    public async Task ValidateInternalLink_ShouldResolveLocallyWhenDomainDoesNotMatchBaseUrl()
    {
        // A link written with a different domain than the configured BaseUrl (e.g. content
        // hardcodes the production URL, or BaseUrl is still the localhost default while
        // testing) should still resolve against the site's own pages instead of being sent
        // out as a real external request.
        var testPage2 = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = "Test Title 2",
        }, "content with [link](https://production.example/test-title)"), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage2);
        var validator = new ValidateLinksCommand(_validateLinksOptionsMock, LoggerMock, _fileSystemMock,
            _httpClientMock, Site);

        await validator.Parse();

        Assert.Empty(validator.PagesWithFailedLinks);
        await _httpClientMock.DidNotReceive().GetAsync(Arg.Any<Uri>());
    }

    [Fact]
    public async Task ValidateInternalLink_ShouldValidateAsExternalWhenStrictBaseUrlIsSet()
    {
        // With StrictBaseUrl, a mismatched-domain link is treated as external even though its
        // path matches a local page, so it's validated as a real HTTP request against that domain.
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        _httpClientMock.GetAsync(Arg.Any<Uri>()).Returns(Task.FromResult(response));

        var testPage2 = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = "Test Title 2",
        }, "content with [link](https://production.example/test-title)"), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage2);
        var options = new ValidateLinksOptions
        {
            CheckExternal = true,
            StrictBaseUrl = true,
        };
        var validator = new ValidateLinksCommand(options, LoggerMock, _fileSystemMock, _httpClientMock, Site);

        await validator.Parse();

        Assert.Single(validator.PagesWithFailedLinks);
        await _httpClientMock.Received().GetAsync(Arg.Is<Uri>(u => u.Host == "production.example"));
    }

    [Fact]
    public async Task ValidateInternalLink_StrictBaseUrlSkipsMismatchedLinkWhenExternalCheckDisabled()
    {
        var testPage2 = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = "Test Title 2",
        }, "content with [link](https://production.example/test-title)"), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage2);
        var options = new ValidateLinksOptions
        {
            CheckExternal = false,
            StrictBaseUrl = true,
        };
        var validator = new ValidateLinksCommand(options, LoggerMock, _fileSystemMock, _httpClientMock, Site);

        await validator.Parse();

        Assert.Empty(validator.PagesWithFailedLinks);
        await _httpClientMock.DidNotReceive().GetAsync(Arg.Any<Uri>());
    }

    [Fact]
    public async Task ValidatePageLinks_ShouldIgnoreMultipleExactLinks()
    {
        var options = new ValidateLinksOptions
        {
            CheckExternal = true,
            Ignore = ["/ignored1", "/ignored2", "https://external.com/ignored3"]
        };

        var testPage = new Page(new(SourcePathConst, new FrontMatter
        {
            Title = "Test Page",
        }, """
           [link1](/ignored1)
           [link2](/ignored2)
           [link3](https://external.com/ignored3)
           """), Site, Site, ("html", null), []);
        Site.PostProcessPage(testPage);

        var validator = new ValidateLinksCommand(options, LoggerMock, _fileSystemMock, _httpClientMock, Site);
        await validator.Parse();

        Assert.Empty(validator.PagesWithFailedLinks);
    }
}
