using System.Reflection;
using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.Parsers;
using Fanstatic.TemplateEngine;
using Serilog;
using Xunit;

namespace Fanstatic.Test.TemplateEngine;

public class FluidTemplateEngineTests : TestSetup
{
    readonly IFileSystem _fs = new FileSystem();

    readonly StopwatchReporter _stopwatch =
        new(new LoggerConfiguration().CreateLogger());

    [Fact]
    public void Render_ShouldCacheCompiledTemplatesByPath()
    {
        var site = CreateSite(TestSitePathConst06);
        var page = GetPage(site, "/index.html");
        var engine = new FluidTemplateEngine();
        engine.Initialize(site);
        var themePath = CreateTemporaryTheme(
            ("one.html", "same body"),
            ("two.html", "same body"));

        try
        {
            Assert.Empty(engine.PreCompileTheme(themePath));
            Assert.Equal(2, GetCacheCount(engine, "_compiledTemplateByPath"));

            Assert.Equal("same body", engine.Render(Path.Combine(themePath, "one.html"), site, page));
            Assert.Equal("same body", engine.Render(Path.Combine(themePath, "two.html"), site, page));
            Assert.Equal("same body", engine.Render(Path.Combine(themePath, "one.html"), site, page));
            Assert.Equal(2, GetCacheCount(engine, "_compiledTemplateByPath"));
            Assert.Equal(0, GetCacheCount(engine, "_compiledInlineTemplateCache"));
        }
        finally
        {
            Directory.Delete(themePath, recursive: true);
        }
    }

    [Fact]
    public void RenderInline_ShouldCacheCompiledTemplatesByBody()
    {
        var site = CreateSite(TestSitePathConst06);
        var page = GetPage(site, "/index.html");
        var engine = new FluidTemplateEngine();
        engine.Initialize(site);
        const string body = "inline body";

        Assert.Equal(body, engine.RenderInline(body, site, page));
        Assert.Equal(body, engine.RenderInline(body, site, page));
        Assert.Equal(1, GetCacheCount(engine, "_compiledInlineTemplateCache"));
        Assert.Equal(0, GetCacheCount(engine, "_compiledTemplateByPath"));
    }

    [Fact]
    public void Initialize_ShouldRecompileChangedFileTemplate()
    {
        var site = CreateSite(TestSitePathConst06);
        var page = GetPage(site, "/index.html");
        var engine = new FluidTemplateEngine();
        var source = Path.Combine(Path.GetTempPath(), $"fanstatic-template-{Guid.NewGuid():N}");
        var themePath = Path.Combine(source, "themes", "test");
        Directory.CreateDirectory(themePath);
        var templatePath = Path.Combine(themePath, "single.html");
        File.WriteAllText(templatePath, "before");
        var engineSite = CreateSiteObject(source);

        try
        {
            engine.Initialize(engineSite);
            Assert.Equal("before", engine.Render(templatePath, site, page));
            Assert.Equal(1, GetCacheCount(engine, "_compiledTemplateByPath"));

            File.WriteAllText(templatePath, "after");
            engine.Initialize(engineSite);

            Assert.Equal(0, GetCacheCount(engine, "_compiledTemplateByPath"));
            Assert.Equal("after", engine.Render(templatePath, site, page));
            Assert.Equal(1, GetCacheCount(engine, "_compiledTemplateByPath"));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    Site CreateSite(string testSitePath)
    {
        var options = new GenerateOptions
        {
            SourceArgument = Path.GetFullPath(Path.Combine(TestSitesPath, testSitePath))
        };

        return SiteHelper.Init(
            "fanstatic.yaml",
            options,
            new YamlParser(),
            LoggerMock,
            _stopwatch,
            _fs);
    }

    Site CreateSiteObject(string sourcePath)
    {
        GenerateOptions options = new() { SourceArgument = sourcePath };
        SiteSettings settings = new() { Theme = "test" };
        return new Site(options, settings, new YamlParser(), LoggerMock, SystemClockMock);
    }

    static string CreateTemporaryTheme(params (string Name, string Body)[] templates)
    {
        var themePath = Path.Combine(Path.GetTempPath(), $"fanstatic-theme-{Guid.NewGuid():N}");
        Directory.CreateDirectory(themePath);
        foreach (var (name, body) in templates)
        {
            File.WriteAllText(Path.Combine(themePath, name), body);
        }

        return themePath;
    }

    static IPage GetPage(Site site, string url)
    {
        Assert.True(site.OutputReferences.TryGetValue(new(url, UriKind.RelativeOrAbsolute), out var output));
        return Assert.IsAssignableFrom<IPage>(output);
    }

    static int GetCacheCount(FluidTemplateEngine engine, string fieldName)
    {
        var field = typeof(FluidTemplateEngine).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var cache = field.GetValue(engine);
        Assert.NotNull(cache);
        var count = cache.GetType().GetProperty("Count");
        Assert.NotNull(count);
        return (int)count.GetValue(cache)!;
    }
}
