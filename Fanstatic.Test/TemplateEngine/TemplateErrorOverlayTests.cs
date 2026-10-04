using Fanstatic.TemplateEngine;
using Xunit;

namespace Fanstatic.Test.TemplateEngine;

public class TemplateErrorOverlayTests
{
    [Fact]
    public void Render_ShouldIncludeErrorDetails()
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

    [Fact]
    public void Render_ShouldEscapeHtml()
    {
        // Arrange
        var error = new TemplateError("/t.html", "<script>alert(1)</script>", 0, 0, null, null);

        // Act
        var html = TemplateErrorOverlay.Render(error);

        // Assert
        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ShouldThrow_WhenErrorIsNull()
    {
        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => TemplateErrorOverlay.Render(null!));
    }
}
