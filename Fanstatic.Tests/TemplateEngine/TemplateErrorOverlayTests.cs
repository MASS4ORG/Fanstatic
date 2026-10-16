using Fanstatic.TemplateEngine;
using Xunit;

namespace Fanstatic.Test.TemplateEngine;

/// <summary>Unit tests for the TemplateErrorOverlay class.</summary>
public class TemplateErrorOverlayTests
{
    /// <summary>Confirms that the overlay includes the error heading, the template path with line and column, and the source page.</summary>
    [Fact]
    public void RenderShouldIncludeErrorDetails()
    {
        // Arrange
        var error = new TemplateError("/t.html", "Unknown tag 'page'", 1, 14,
            "INDEX-{% page.ContentPreRendered %}", "index.md");

        // Act
        var html = TemplateErrorOverlay.Render(error);

        // Assert
        Assert.Contains("Template error", html, StringComparison.Ordinal);
        Assert.Contains("&#39;/t.html:1:14&#39;", html, StringComparison.Ordinal);
        Assert.Contains("(rendering &#39;index.md&#39;)", html, StringComparison.Ordinal);
    }

    /// <summary>Confirms that error text is HTML-escaped so raw markup never reaches the output.</summary>
    [Fact]
    public void RenderShouldEscapeHtml()
    {
        // Arrange
        var error = new TemplateError("/t.html", "<script>alert(1)</script>", 0, 0, null, null);

        // Act
        var html = TemplateErrorOverlay.Render(error);

        // Assert
        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    /// <summary>Confirms that rendering a null error throws an ArgumentNullException.</summary>
    [Fact]
    public void RenderShouldThrowWhenErrorIsNull()
    {
        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => TemplateErrorOverlay.Render(null!));
    }
}
