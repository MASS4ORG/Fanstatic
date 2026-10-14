using Fanstatic.Helpers;
using Xunit;

namespace Fanstatic.Test.Helpers;

public class UrlExtensionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void UrlizeNullOrEmptyTextThrowsArgumentNullException(string? text)
    {
        var result = UrlExtension.ConvertToUrlFriendly(text);
        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void UrlizePathNullPathReturnsEmptyString(string? path)
    {
        var result = UrlExtension.SanitizeUrlPath(path);

        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData("Hello, World!", '-', true, false, "hello-world")]
    [InlineData("Hello, World!", '_', true, false, "hello_world")]
    [InlineData("Hello, World!", '-', false, false, "Hello-World")]
    [InlineData("Hello.World", '-', true, false, "hello.world")]
    [InlineData("Hello.World", '-', true, true, "hello-world")]
    public void UrlizeValidTextReturnsExpectedResult(
        string text,
        char? replacementChar,
        bool lowerCase,
        bool replaceDot,
        string expectedResult)
    {
        var options = new UrlSanitizationOptions
        {
            ReplacementChar = replacementChar,
            LowerCase = lowerCase,
            ReplaceDot = replaceDot
        };
        var result = UrlExtension.ConvertToUrlFriendly(text, options);

        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData("Documents/My Report.docx", '-', true, false, "documents/my-report.docx")]
    [InlineData("Documents/My Report.docx", '_', true, false, "documents/my_report.docx")]
    [InlineData("Documents/My Report.docx", '-', false, false, "Documents/My-Report.docx")]
    [InlineData("Documents/My Report.docx", '-', true, true, "documents/my-report-docx")]
    [InlineData("C:/Documents/My Report.docx", '_', true, true, "c/documents/my_report_docx")]
    [InlineData("Documents/My Report.docx", null, true, false, "documents/myreport.docx")]
    public void UrlizePathValidPathReturnsExpectedResult(string path, char? replacementChar, bool lowerCase,
        bool replaceDot, string expectedResult)
    {
        var options = new UrlSanitizationOptions
        { ReplacementChar = replacementChar, LowerCase = lowerCase, ReplaceDot = replaceDot };
        var result = UrlExtension.SanitizeUrlPath(path, options);

        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void UrlizeWithoutOptionsReturnsExpectedResult()
    {
        const string text = "Hello, World!";
        var result = UrlExtension.ConvertToUrlFriendly(text);

        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void UrlizePathWithoutOptionsReturnsExpectedResult()
    {
        const string path = "Documents/My Report.docx";
        var result = UrlExtension.SanitizeUrlPath(path);

        Assert.Equal("documents/my-report.docx", result);
    }

    [Fact]
    public void UrlizeSpecialCharsInTextReturnsOnlyHyphens()
    {
        const string text = "!@#$%^&*()";
        var result = UrlExtension.ConvertToUrlFriendly(text);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void UrlizePathSpecialCharsInPathReturnsOnlyHyphens()
    {
        const string path = "/!@#$%^&*()/";
        var result = UrlExtension.SanitizeUrlPath(path);

        Assert.Equal("/", result);
    }

    [Theory]
    [InlineData("/", "/index.html", "/index.html")]
    [InlineData("/page", "/page/index.html", "/page/index.html")]
    [InlineData("/page/", "/page/index.html", "/page/index.html")]
    [InlineData("/page.html", "/page.html", "/page.html")]
    [InlineData("page", "/page/index.html", "/page/index.html")]
    [InlineData("page/", "/page/index.html", "/page/index.html")]
    [InlineData("page.html", "/page.html", "/page.html")]
    [InlineData("/blog/v2.2.0", "/blog/v2.2.0/index.html", "/blog/v2.2.0/index.html")]
    public void CorrectRequestPathBasicPathsReturnsExpectedResults(string input, string expectedStripped,
        string expectedFull)
    {
        var inputUri = new Uri(input, UriKind.Relative);
        var (stripped, full) = UrlExtension.CorrectRequestPath(inputUri);

        Assert.Equal(expectedStripped, stripped.ToString());
        Assert.Equal(expectedFull, full.ToString());
    }

    [Theory]
    [InlineData("/page?key=value", "/page/index.html", "/page/index.html?key=value")]
    [InlineData("/page/?key=value", "/page/index.html", "/page/index.html?key=value")]
    [InlineData("/page.html?key=value", "/page.html", "/page.html?key=value")]
    [InlineData("page?key=value&key2=value2", "/page/index.html", "/page/index.html?key=value&key2=value2")]
    public void CorrectRequestPathWithQueryStringReturnsExpectedResults(string input, string expectedStripped,
        string expectedFull)
    {
        var inputUri = new Uri(input, UriKind.Relative);
        var (stripped, full) = UrlExtension.CorrectRequestPath(inputUri);

        Assert.Equal(expectedStripped, stripped.ToString());
        Assert.Equal(expectedFull, full.ToString());
    }

    [Theory]
    [InlineData("/page#section", "/page/index.html", "/page/index.html#section")]
    [InlineData("/page/#section", "/page/index.html", "/page/index.html#section")]
    [InlineData("/page.html#section", "/page.html", "/page.html#section")]
    [InlineData("page#section", "/page/index.html", "/page/index.html#section")]
    [InlineData("#section", "/index.html", "/index.html#section")]
    public void CorrectRequestPathWithAnchorReturnsExpectedResults(string input, string expectedStripped,
        string expectedFull)
    {
        var inputUri = new Uri(input, UriKind.Relative);
        var (stripped, full) = UrlExtension.CorrectRequestPath(inputUri);

        Assert.Equal(expectedStripped, stripped.ToString());
        Assert.Equal(expectedFull, full.ToString());
    }

    [Theory]
    [InlineData("/page-slash?key=value#section", "/page-slash/index.html", "/page-slash/index.html?key=value#section")]
    [InlineData("/page-slash.html?key=value#section", "/page-slash.html", "/page-slash.html?key=value#section")]
    [InlineData("page-simple?key=value#section", "/page-simple/index.html",
        "/page-simple/index.html?key=value#section")]
    [InlineData("?key=value#section", "/index.html", "/index.html?key=value#section")]
    [InlineData("#section?key=value", "/index.html", "/index.html#section?key=value")]
    public void CorrectRequestPathWithQueryAndAnchorReturnsExpectedResults(string input, string expectedStripped,
        string expectedFull)
    {
        var inputUri = new Uri(input, UriKind.Relative);
        var (stripped, full) = UrlExtension.CorrectRequestPath(inputUri);

        Assert.Equal(expectedStripped, stripped.ToString());
        Assert.Equal(expectedFull, full.ToString());
    }

    [Theory]
    [InlineData("https://example.com/page", "/page/index.html", "/page/index.html")]
    [InlineData("https://example.com/page?key=value", "/page/index.html", "/page/index.html?key=value")]
    [InlineData("https://example.com/page#section", "/page/index.html", "/page/index.html#section")]
    [InlineData("https://example.com/page?key=value#section", "/page/index.html", "/page/index.html?key=value#section")]
    public void CorrectRequestPathAbsoluteUriReturnsRelativeUri(string input, string expectedStripped,
        string expectedFull)
    {
        var inputUri = new Uri(input, UriKind.Absolute);
        var (stripped, full) = UrlExtension.CorrectRequestPath(inputUri);

        Assert.Equal(expectedStripped, stripped.ToString());
        Assert.Equal(expectedFull, full.ToString());
    }

    [Fact]
    public void CorrectRequestPathNullThrowsArgumentNullException()
    {
        var inputUri = null as Uri;

        Assert.Throws<ArgumentNullException>(() =>
            UrlExtension.CorrectRequestPath(inputUri!));
    }

    [Fact]
    public void CorrectRequestPathEmptyStringReturnsRootIndex()
    {
        var inputUri = new Uri("", UriKind.Relative);
        var (stripped, full) = UrlExtension.CorrectRequestPath(inputUri);

        Assert.Equal("/index.html", stripped.ToString());
        Assert.Equal("/index.html", full.ToString());
    }

    [Theory]
    [InlineData("/blog", "post1", "/blog/post1")]
    [InlineData("/blog/", "post1", "/blog/post1")]
    [InlineData("/blog/index.html", "post1", "/blog/post1")]
    [InlineData("/", "page", "/page")]
    [InlineData("/index.html", "page", "/page")]
    public void CombineRelativeValidPathsReturnsExpectedResults(string basePath, string relativePath, string expected)
    {
        var baseUri = new Uri(basePath, UriKind.Relative);
        var relativeUri = new Uri(relativePath, UriKind.Relative);

        var result = UrlExtension.CombineRelative(baseUri, relativeUri);

        Assert.Equal(expected, result.ToString());
    }

    [Theory]
    [InlineData("/blog", "/post1", "/post1")]
    [InlineData("/blog/", "/post1/", "/post1/")]
    [InlineData("/blog/index.html", "/post1/", "/post1/")]
    [InlineData("/blog/index.html", "/post1/post2", "/post1/post2")]
    [InlineData("/blog/index.html", "./post1/post2", "/blog/post1/post2")]
    [InlineData("/blog/index.html", "../post1/post2", "/post1/post2")]
    [InlineData("/blog/post1/index.html", "../../post2", "/post2")]
    public void CombineRelativeHandlesLeadingAndTrailingSlashes(string basePath, string relativePath, string expected)
    {
        var baseUri = new Uri(basePath, UriKind.Relative);
        var relativeUri = new Uri(relativePath, UriKind.Relative);

        var result = UrlExtension.CombineRelative(baseUri, relativeUri);

        Assert.Equal(expected, result.ToString());
    }
}
