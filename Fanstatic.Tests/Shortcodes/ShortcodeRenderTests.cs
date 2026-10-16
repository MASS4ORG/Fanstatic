using Fanstatic.Commands.Build;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.Parsers;
using Fanstatic.Shortcodes;
using NSubstitute;
using Serilog;
using Xunit;

namespace Fanstatic.Test.Shortcodes;

/// <summary>
/// Tests for rendering shortcodes in content, using the <c>17-shortcodes</c> fixture and temporary sites.
/// </summary>
public class ShortcodeRenderTests : TestSetup
{
    const string FixturePath = ".TestSites/17-shortcodes";

    readonly Site _site;

    /// <summary>Builds the shortcode fixture site.</summary>
    public ShortcodeRenderTests()
    {
        var options = new BuildOptions
        {
            SourceArgument = Path.GetFullPath(Path.Combine(TestSitesPath, FixturePath)),
            Output = string.Empty
        };
        _site = SiteHelper.Init("fanstatic.yaml", options, new YamlParser(), LoggerMock,
            new StopwatchReporter(LoggerMock), new FileSystem());
    }

    Page FixturePage(string sourceRelativePath, string outputFormat = "html") =>
        _site.OutputReferences.Values
            .OfType<Page>()
            .First(page => page.SourceRelativePath == sourceRelativePath
                           && page.OutputFormat == outputFormat
                           && page.PageIndex == 1);

    /// <summary>Confirms that a standard-notation shortcode renders with the caller page and without a paragraph wrapper.</summary>
    [Fact]
    public void StandardShortcodeShouldRenderWithoutParagraph()
    {
        var html = FixturePage("posts/audio.md").ContentPreRendered;

        Assert.Equal(
            "<p>Intro paragraph.</p>\n<audio controls preload=\"auto\" src=\"/audio/test.mp3\" title=\"Audio Post\"></audio>",
            html.TrimEnd());
    }

    /// <summary>Confirms that the language-specific template is picked for a translated page.</summary>
    [Fact]
    public void LookupShouldPreferLanguageVariant()
    {
        var html = FixturePage("posts/audio.pt-br.md").ContentPreRendered;

        Assert.Contains("title=\"Post de Áudio\" lang=\"pt-br\"", html, StringComparison.Ordinal);
    }

    /// <summary>Confirms that the output-format template is picked for the RSS variant.</summary>
    [Fact]
    public void LookupShouldPreferOutputFormatVariant()
    {
        var html = FixturePage("posts/audio.md", "rss").ContentPreRendered;

        Assert.Contains("<enclosure url=\"/audio/test.mp3\" type=\"audio/mpeg\" />", html, StringComparison.Ordinal);
    }

    /// <summary>Confirms that a site shortcode overrides the theme shortcode of the same name.</summary>
    [Fact]
    public void SiteShortcodeShouldOverrideTheme()
    {
        var html = FixturePage("posts/nested.md").ContentPreRendered;

        Assert.Contains("class=\"note site\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("note theme", html, StringComparison.Ordinal);
    }

    /// <summary>Confirms that nested calls render inside out, see their parent, and that Markdown notation goes through Markdig.</summary>
    [Fact]
    public void NestedShortcodesShouldRenderInsideOut()
    {
        var html = FixturePage("posts/nested.md").ContentPreRendered;

        Assert.Contains(
            "<div class=\"note site\" data-ordinal=\"0\"><strong>Bold</strong> text " +
            "<video src=\"/v.mp4\" data-parent=\"note\" data-ordinal=\"0\"></video></div>",
            html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"note site\" data-ordinal=\"1\">second</div>", html, StringComparison.Ordinal);
    }

    /// <summary>Confirms that ref resolves next to custom shortcodes in the same paragraph.</summary>
    [Fact]
    public void RefShouldWorkNextToCustomShortcodes()
    {
        var html = FixturePage("posts/refs.md").ContentPreRendered;
        var target = FixturePage("posts/audio.md");

        Assert.Contains($"<a href=\"{target.RelPermalink}\">the audio post</a>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"note site\" data-ordinal=\"1\">a note</div>", html, StringComparison.Ordinal);
    }

    /// <summary>Confirms that escaped shortcodes are output literally.</summary>
    [Fact]
    public void EscapedShortcodesShouldRenderLiterally()
    {
        var html = FixturePage("posts/escaped.md").ContentPreRendered;

        Assert.Contains("<code>{{&lt; audio src=&quot;x&quot; &gt;}}</code>", html, StringComparison.Ordinal);
        Assert.Contains("<code>{{% note %}}</code>", html, StringComparison.Ordinal);
    }

    /// <summary>Confirms that page.Shortcodes lists the called shortcodes so layouts can load assets.</summary>
    [Fact]
    public void PageShortcodesShouldListCalledShortcodes()
    {
        Assert.Equal(["audio"], FixturePage("posts/audio.md").Shortcodes);
        Assert.Equal(["media/video", "note"], FixturePage("posts/nested.md").Shortcodes);
        Assert.Equal(["note"], FixturePage("posts/directive.md").Shortcodes);
        Assert.Empty(FixturePage("posts/escaped.md").Shortcodes);
        Assert.EndsWith("-AUDIO-ASSETS", FixturePage("posts/audio.md").Content, StringComparison.Ordinal);
    }

    /// <summary>Confirms that an unknown shortcode is reported with the content file, line and column, and fails the render.</summary>
    [Fact]
    public void UnknownShortcodeShouldReportFileLineAndColumn()
    {
        using var site = new ShortcodeTestSite(LoggerMock, new Dictionary<string, string>
        {
            ["content/broken.md"] = "---\ntitle: Broken\n---\nLine one.\n\nText {{< missing >}}\n"
        });
        var page = site.Page("broken.md");

        var exception = Assert.Throws<ShortcodeException>(() => page.ContentPreRendered);
        Assert.Equal((6, 6), (exception.Line, exception.Column));
        Assert.Equal(Path.Combine(site.Site.SourceContentPath, "broken.md"), exception.SourcePath);
        Assert.Equal("Text {{< missing >}}", exception.SourceExcerpt);

        Assert.Equal(string.Empty, page.CompleteContent);
        var error = Assert.Single(site.Site.TemplateErrors);
        Assert.Equal($"'{exception.SourcePath}:6:6'", error.Location);
        Assert.Equal("shortcode 'missing' not found", error.Message);
        Assert.Equal("broken.md", error.PageSourceRelativePath);
    }

    /// <summary>Confirms that the dev server shows the shortcode failure in the error overlay.</summary>
    [Fact]
    public void ShortcodeErrorShouldShowOverlayInServe()
    {
        using var site = new ShortcodeTestSite(LoggerMock, new Dictionary<string, string>
        {
            ["content/broken.md"] = "---\ntitle: Broken\n---\n{{< missing >}}\n"
        });
        site.Site.Fanstatic.IsServer = true;

        var output = site.Page("broken.md").CompleteContent;

        Assert.Contains("<h1>Template error</h1>", output, StringComparison.Ordinal);
        Assert.Contains("broken.md:4:1", output, StringComparison.Ordinal);
    }

    /// <summary>Confirms that mixing named and positional arguments fails as in Hugo.</summary>
    [Fact]
    public void MixedArgumentsShouldFail()
    {
        using var site = new ShortcodeTestSite(LoggerMock, new Dictionary<string, string>
        {
            ["shortcodes/audio.html"] = "<audio src=\"{{ src }}\"></audio>",
            ["content/mixed.md"] = "---\ntitle: Mixed\n---\n{{< audio \"x\" src=\"y\" >}}\n"
        });

        var exception = Assert.Throws<ShortcodeException>(() => site.Page("mixed.md").ContentPreRendered);

        Assert.Equal("shortcode 'audio': cannot mix named and positional arguments", exception.Message);
        Assert.Equal((4, 1), (exception.Line, exception.Column));
    }

    /// <summary>Confirms that errorf fails the shortcode with its position and warnf logs a warning.</summary>
    [Fact]
    public void ErrorfAndWarnfShouldReportFromShortcodes()
    {
        using var site = new ShortcodeTestSite(LoggerMock, new Dictionary<string, string>
        {
            ["shortcodes/fail.html"] = "{% unless src %}{{ \"%s requires src, got %q%% %x\" | errorf: shortcode.Name, 1 }}{% endunless %}",
            ["shortcodes/warn.html"] = "ok{{ \"old %s %s\" | warnf: shortcode.Name }}",
            ["content/fail.md"] = "---\ntitle: Fail\n---\n{{< fail >}}\n",
            ["content/warn.md"] = "---\ntitle: Warn\n---\n{{< warn >}}\n"
        });

        var exception = Assert.Throws<ShortcodeException>(() => site.Page("fail.md").ContentPreRendered);
        Assert.Equal("shortcode 'fail': fail requires src, got \"1\"% %x", exception.Message);

        Assert.Equal("ok", site.Page("warn.md").ContentPreRendered.TrimEnd());
        LoggerMock.Received().Warning("{Message} (rendering {File})", "old warn %!s(MISSING)", "warn.md");
    }

    /// <summary>Confirms the shortcode variables: arguments, inner, params, position, deindented inner and the caller page.</summary>
    [Fact]
    public void TemplateShouldSeeShortcodeVariables()
    {
        using var site = new ShortcodeTestSite(LoggerMock, new Dictionary<string, string>
        {
            ["shortcodes/show.html"] =
                "[{{ a }}|{{ params.a }}|{{ shortcode.IsNamedParams }}|{{ shortcode.Name }}|{{ page.Title }}|" +
                "{{ shortcode.Position }}|{{ shortcode.InnerDeindent }}|{{ inner | size }}]",
            ["shortcodes/pos.html"] = "[{{ params[1] }}|{{ shortcode.IsNamedParams }}|{{ shortcode.Params | size }}]",
            ["content/vars.md"] =
                "---\ntitle: Vars\n---\n{{< show a=\"1\" page=\"ignored\" >}}\n    x\n      y\n{{< /show >}}\n\n" +
                "{{< pos \"p0\" \"p1\" >}}\n"
        });

        var html = site.Page("vars.md").ContentPreRendered;

        var position = Path.Combine(site.Site.SourceContentPath, "vars.md") + ":4:1";
        Assert.Contains($"[1|1|true|show|Vars|{position}|\nx\n  y\n|15]", html, StringComparison.Ordinal);
        Assert.Contains("[p1|false|2]", html, StringComparison.Ordinal);
    }

    /// <summary>Confirms that markdownify drops the paragraph wrapper of a single paragraph unless asked to keep it.</summary>
    [Fact]
    public void MarkdownifyShouldStripSingleParagraph()
    {
        using var site = new ShortcodeTestSite(LoggerMock, new Dictionary<string, string>
        {
            ["shortcodes/md.html"] = "[{{ inner | markdownify }}][{{ inner | markdownify: \"block\" }}][{{ \"a\n\nb\" | markdownify }}]",
            ["content/md.md"] = "---\ntitle: Md\n---\n{{< md >}}*x*{{< /md >}}\n"
        });

        var html = site.Page("md.md").ContentPreRendered;

        Assert.Contains("[<em>x</em>][<p><em>x</em></p>][<p>a</p>\n<p>b</p>]", html, StringComparison.Ordinal);
    }

    /// <summary>Confirms that placeholder-like text in content is left untouched.</summary>
    [Fact]
    public void PlaceholderLikeTextShouldStay()
    {
        using var site = new ShortcodeTestSite(LoggerMock, new Dictionary<string, string>
        {
            ["shortcodes/x.html"] = "X",
            ["content/p.md"] = "---\ntitle: P\n---\nFANSTATICSC FANSTATICSC9X FANSTATICSC1 {{< x >}}\n"
        });

        var html = site.Page("p.md").ContentPreRendered;

        Assert.Equal("<p>FANSTATICSC FANSTATICSC9X FANSTATICSC1 X</p>", html.TrimEnd());
    }

    /// <summary>Confirms that content without shortcodes renders as plain Markdown and lists no shortcodes.</summary>
    [Fact]
    public void ContentWithoutShortcodesShouldRenderAsMarkdown()
    {
        using var site = new ShortcodeTestSite(LoggerMock, new Dictionary<string, string>
        {
            ["content/plain.md"] = "---\ntitle: Plain\n---\n*plain* {{ liquid }}\n"
        });
        var page = site.Page("plain.md");

        Assert.Equal("<p><em>plain</em> {{ liquid }}</p>", page.ContentPreRendered.TrimEnd());
        Assert.Empty(page.Shortcodes);
        Assert.Empty(ShortcodeRenderer.CollectNames("{{< broken", site.Site, page));
    }

    /// <summary>Confirms the per-folder file lookup order for HTML and other output formats.</summary>
    [Fact]
    public void CandidateFileNamesShouldFollowHugoOrder()
    {
        Assert.Equal(
            ["audio.pt-br.html.html", "audio.html.html", "audio.pt-br.html", "audio.html"],
            ShortcodeRegistry.CandidateFileNames("audio", "pt-br", "html"));
        Assert.Equal(
            ["audio.en.rss.xml", "audio.rss.xml", "audio.en.xml", "audio.xml", "audio.en.html", "audio.html"],
            ShortcodeRegistry.CandidateFileNames("audio", "en", "rss"));
        Assert.Equal(["x.custom.custom", "x.custom", "x.html"], ShortcodeRegistry.CandidateFileNames("x", null, "custom"));
    }

    /// <summary>Confirms that names escaping the shortcode folders resolve to nothing and that Clear re-reads templates.</summary>
    [Fact]
    public void RegistryShouldRejectUnsafeNamesAndReloadAfterClear()
    {
        using var site = new ShortcodeTestSite(LoggerMock, new Dictionary<string, string>
        {
            ["shortcodes/a.html"] = "one",
            ["content/a.md"] = "---\ntitle: A\n---\n"
        });
        var registry = site.Site.ShortcodeRegistry;

        Assert.Null(registry.Resolve("../fanstatic", "en", "html"));
        Assert.Null(registry.Resolve("/a", "en", "html"));
        Assert.Null(registry.Resolve("a//b", "en", "html"));
        Assert.False(registry.Resolve("a", "en", "html")!.UsesInner);

        File.WriteAllText(Path.Combine(site.Source, "shortcodes", "a.html"), "{{ inner }}");
        Assert.False(registry.Resolve("a", "en", "html")!.UsesInner);
        registry.Clear();
        Assert.True(registry.Resolve("a", "en", "html")!.UsesInner);
    }

    /// <summary>Confirms that rendering from a template fails cleanly for unknown shortcodes and wrong arguments.</summary>
    [Fact]
    public void RenderFromTemplateShouldReportFailures()
    {
        using var site = new ShortcodeTestSite(LoggerMock, new Dictionary<string, string>
        {
            ["content/a.md"] = "---\ntitle: A\n---\n"
        });
        var page = site.Page("a.md");

        Assert.Equal("shortcode 'nope' not found",
            Assert.Throws<ShortcodeException>(() => ShortcodeRenderer.RenderFromTemplate("nope", [], null, site.Site, page))
                .Message);
        Assert.Equal("shortcode 'ref': too many positional arguments: expected at most 1 (path)",
            Assert.Throws<ShortcodeException>(() =>
                ShortcodeRenderer.RenderFromTemplate("ref", [new(null, "a"), new(null, "b")], null, site.Site, page))
                .Message);
    }

    /// <summary>Confirms that the shortcode exception location is kept when a page fails inside another page.</summary>
    [Fact]
    public void LocatedExceptionShouldKeepItsLocation()
    {
        var exception = new ShortcodeException("boom", 3).WithLocation("file.md", 2, 5, "line");

        Assert.Equal(("file.md", 2, 5, "line", 3), (exception.SourcePath, exception.Line, exception.Column,
            exception.SourceExcerpt, exception.Offset));
    }

    /// <summary>Confirms that a site without a theme still renders file shortcodes.</summary>
    [Fact]
    public void ShortcodesShouldRenderWithoutTheme()
    {
        var source = Path.Combine(Path.GetTempPath(), $"fanstatic-shortcodes-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(source, "content"));
        Directory.CreateDirectory(Path.Combine(source, "shortcodes"));
        File.WriteAllText(Path.Combine(source, "fanstatic.yaml"), "Title: test\n");
        File.WriteAllText(Path.Combine(source, "shortcodes", "hi.html"), "hi {{ page.Title }}");
        File.WriteAllText(Path.Combine(source, "content", "a.md"), "---\ntitle: A\n---\n{{< hi >}}\n");

        try
        {
            var logger = Substitute.For<ILogger>();
            var site = SiteHelper.Init("fanstatic.yaml", new BuildOptions { SourceArgument = source, Output = "" },
                new YamlParser(), logger, new StopwatchReporter(logger), new FileSystem());
            var page = site.OutputReferences.Values.OfType<Page>()
                .First(candidate => candidate.SourceRelativePath == "a.md" && candidate.OutputFormat == "html");

            Assert.Equal("hi A", page.ContentPreRendered.TrimEnd());
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }
}
