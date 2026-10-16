using Fanstatic.Commands.Build;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.Parsers;
using Xunit;

namespace Fanstatic.Test.Shortcodes;

/// <summary>
/// Tests for the <c>{% shortcode %}</c> Liquid tag used in layouts.
/// </summary>
public class ShortcodeTemplateTagTests : TestSetup
{
    ShortcodeTestSite CreateSite(string layout, params (string Path, string Content)[] files)
    {
        var all = files.ToDictionary(file => file.Path, file => file.Content);
        all["themes/test/_default/single.html"] = layout;
        all.TryAdd("content/a.md", "---\ntitle: A\nparams:\n  audio: /a.mp3\n  tags: [x, y]\n---\nbody\n");
        return new(LoggerMock, all);
    }

    /// <summary>Confirms that the fixture layout renders a standalone tag and a block tag with Markdown inner content.</summary>
    [Fact]
    public void TagShouldRenderStandaloneAndBlockFormsInFixture()
    {
        var options = new BuildOptions
        {
            SourceArgument = Path.GetFullPath(Path.Combine(TestSitesPath, ".TestSites/17-shortcodes")),
            Output = string.Empty
        };
        var site = SiteHelper.Init("fanstatic.yaml", options, new YamlParser(), LoggerMock,
            new StopwatchReporter(LoggerMock), new FileSystem());
        var page = site.OutputReferences.Values.OfType<Page>()
            .First(candidate => candidate.SourceRelativePath == "posts/layout.md" && candidate.OutputFormat == "html");

        var content = page.Content;

        Assert.Empty(site.TemplateErrors);
        Assert.Contains("<audio controls preload=\"auto\" src=\"/audio/layout.mp3\" title=\"Layout\"></audio>", content,
            StringComparison.Ordinal);
        Assert.Contains("<div class=\"note site\" data-ordinal=\"0\">Layout <strong>note</strong> for Layout</div>",
            content, StringComparison.Ordinal);
    }

    /// <summary>Confirms that argument values are Liquid expressions converted to typed values, and nil values are skipped.</summary>
    [Fact]
    public void TagShouldEvaluateArgumentExpressions()
    {
        using var site = CreateSite(
            "{% shortcode 'conv', n: 2, big: 3000000000, f: 1.5, b: true, list: page.Params.tags, s: page.Params.audio, p: page, missing: page.Params.nope %}",
            ("shortcodes/conv.html",
                "[{{ n | plus: 1 }}|{{ big }}|{{ f }}|{{ b }}|{{ list | join: ',' }}|{{ s }}|{{ p.Title }}|{{ shortcode.NamedParams | size }}]"));

        var content = site.Page("a.md").Content;

        Assert.Equal("[3|3000000000|1.5|true|x,y|/a.mp3|A|7]", content);
    }

    /// <summary>Confirms that positional arguments, built-ins and dynamic names work in the standalone form.</summary>
    [Fact]
    public void TagShouldAcceptPositionalArgumentsAndDynamicNames()
    {
        using var site = CreateSite(
            "{% shortcode 'relref', 'a.md' %}|{% assign name = 'hi' %}{% shortcode name %}",
            ("shortcodes/hi.html", "hi {{ page.Title }}"));

        var content = site.Page("a.md").Content;

        Assert.Equal($"{site.Page("a.md").RelPermalink}|hi A", content);
    }

    /// <summary>Confirms that a block tag without its end tag is a theme error.</summary>
    [Fact]
    public void BlockTagWithoutEndShouldFailToParse()
    {
        using var site = CreateSite("{% shortcode 'box' %}inner", ("shortcodes/box.html", "[{{ inner }}]"));

        var error = Assert.Single(site.Site.TemplateErrors);

        Assert.Contains("'{% endshortcode %}' was expected", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Confirms that a failing tag is reported against the layout.</summary>
    [Fact]
    public void FailingTagShouldReportTheLayout()
    {
        using var site = CreateSite("{% shortcode 'nope' %}");

        _ = site.Page("a.md").Content;

        var error = Assert.Single(site.Site.TemplateErrors);
        Assert.Equal("shortcode 'nope' not found", error.Message);
        Assert.EndsWith("single.html", error.TemplatePath, StringComparison.Ordinal);
    }
}
