using Fanstatic.TemplateEngine;
using Xunit;

namespace Fanstatic.Test.TemplateEngine;

public class TemplateErrorTests
{
    [Fact]
    public void FromFluidParseErrorShouldSplitPositionAndExcerpt()
    {
        // Act
        var error = TemplateError.FromFluidParseError("/themes/test/_default/single.html",
            "Invalid 'if' tag at (3:6)\nSource:\n{% if %}x{% endif %}", null);

        // Assert
        Assert.Equal("Invalid 'if' tag", error.Message);
        Assert.Equal(3, error.Line);
        Assert.Equal(6, error.Column);
        Assert.Equal("{% if %}x{% endif %}", error.SourceExcerpt);
        Assert.Null(error.PageSourceRelativePath);
    }

    [Fact]
    public void FromFluidParseErrorShouldDeriveExcerptFromBodyWhenEngineOmitsIt()
    {
        // Act
        var error = TemplateError.FromFluidParseError("/themes/test/_default/index.html",
            "Unknown tag 'page' at (1:14)", "INDEX-{% page.ContentPreRendered %}");

        // Assert
        Assert.Equal("Unknown tag 'page'", error.Message);
        Assert.Equal(1, error.Line);
        Assert.Equal(14, error.Column);
        Assert.Equal("INDEX-{% page.ContentPreRendered %}", error.SourceExcerpt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromFluidParseErrorShouldUseFallbackMessageWhenEngineReportsNothing(string? parseError)
    {
        // Act
        var error = TemplateError.FromFluidParseError("/themes/test/_default/index.html", parseError, null);

        // Assert
        Assert.Equal("Template could not be parsed.", error.Message);
        Assert.Equal(0, error.Line);
        Assert.Equal(0, error.Column);
        Assert.Null(error.SourceExcerpt);
    }

    [Fact]
    public void FromFluidParseErrorShouldIgnorePositionWhenFormatIsUnexpected()
    {
        // Act
        var error = TemplateError.FromFluidParseError("/t.html", "Broken at (a:b)", null);

        // Assert
        Assert.Equal("Broken at (a:b)", error.Message);
        Assert.Equal(0, error.Line);
        Assert.Equal(0, error.Column);
    }

    [Fact]
    public void FromFluidParseErrorShouldReturnNullExcerptWhenLineIsOutOfRange()
    {
        // Act
        var error = TemplateError.FromFluidParseError("/t.html", "Bad at (9:1)", "one\ntwo");

        // Assert
        Assert.Null(error.SourceExcerpt);
    }

    [Fact]
    public void FromRenderExceptionShouldNameTemplateAndPageForRuntimeError()
    {
        // Act
        var error = TemplateError.FromRenderException("/themes/test/_default/single.html",
            "blog/post-1.md", new FileNotFoundException("partials/missing.html"));

        // Assert
        Assert.Equal("/themes/test/_default/single.html", error.TemplatePath);
        Assert.Equal("partials/missing.html", error.Message);
        Assert.Equal("blog/post-1.md", error.PageSourceRelativePath);
        Assert.Equal(0, error.Line);
    }

    [Fact]
    public void FromRenderExceptionShouldKeepPositionForFormatException()
    {
        // Act
        var error = TemplateError.FromRenderException("/themes/test/_default/index.html",
            "index.md", new FormatException("Unknown tag 'page' at (1:14)"));

        // Assert
        Assert.Equal("Unknown tag 'page'", error.Message);
        Assert.Equal(1, error.Line);
        Assert.Equal(14, error.Column);
        Assert.Equal("index.md", error.PageSourceRelativePath);
    }

    [Fact]
    public void FromRenderExceptionShouldThrowWhenExceptionIsNull()
    {
        // Act and Assert
        Assert.Throws<ArgumentNullException>(() =>
            TemplateError.FromRenderException("/t.html", "index.md", null!));
    }

    [Fact]
    public void LocationShouldIncludePositionWhenKnown()
    {
        // Act
        var error = new TemplateError("/t.html", "Boom", 4, 2, null, null);

        // Assert
        Assert.Equal("'/t.html:4:2'", error.Location);
    }

    [Fact]
    public void LocationShouldOmitPositionWhenUnknown()
    {
        // Act
        var error = new TemplateError("/t.html", "Boom", 0, 0, null, null);

        // Assert
        Assert.Equal("'/t.html'", error.Location);
    }

    [Fact]
    public void ToStringShouldIncludePageAndExcerpt()
    {
        // Act
        var error = new TemplateError("/t.html", "Boom", 4, 2, "{% bad %}", "blog/post-1.md");

        // Assert
        Assert.Equal("'/t.html:4:2': Boom (rendering 'blog/post-1.md')\n  {% bad %}", error.ToString());
    }

    [Fact]
    public void ToStringShouldOmitPageWhenValidationFailed()
    {
        // Act
        var error = new TemplateError("/t.html", "Boom", 0, 0, null, null);

        // Assert
        Assert.Equal("'/t.html': Boom", error.ToString());
    }

    [Fact]
    public void FromRenderExceptionShouldKeepPositionWhenRenderFailureIsLocated()
    {
        // Arrange
        var inner = new FileNotFoundException("partials/footer.html");
        var body = "line one\n<div>\n  {% include 'partials/footer.html' %}\n</div>\n";
        var exception = TemplateRenderException.MissingTemplate("partials/footer.html", body, inner);

        // Act
        var error = TemplateError.FromRenderException("/_default/baseof.html", "homepage/2-simplicity.md", exception);

        // Assert
        Assert.Equal("template not found: 'partials/footer.html'", error.Message);
        Assert.Equal(3, error.Line);
        Assert.Equal(3, error.Column);
        Assert.Equal("{% include 'partials/footer.html' %}", error.SourceExcerpt);
        Assert.Equal("homepage/2-simplicity.md", error.PageSourceRelativePath);
    }

    [Fact]
    public void FromRenderExceptionShouldOmitPositionWhenMissingTemplateIsNotReferenced()
    {
        // Arrange
        var exception = TemplateRenderException.MissingTemplate("partials/footer.html", "no tags here",
            new FileNotFoundException("partials/footer.html"));

        // Act
        var error = TemplateError.FromRenderException("/_default/baseof.html", "homepage/2-simplicity.md", exception);

        // Assert
        Assert.Equal(0, error.Line);
        Assert.Equal(0, error.Column);
        Assert.Null(error.SourceExcerpt);
    }

    [Fact]
    public void MissingTemplateShouldThrowWhenArgumentIsNull()
    {
        // Act and Assert
        Assert.Throws<ArgumentNullException>(() =>
            TemplateRenderException.MissingTemplate(null!, "body", new FileNotFoundException("x")));
        Assert.Throws<ArgumentNullException>(() =>
            TemplateRenderException.MissingTemplate("p.html", null!, new FileNotFoundException("x")));
        Assert.Throws<ArgumentNullException>(() =>
            TemplateRenderException.MissingTemplate("p.html", "body", null!));
    }
}
