using System.Reflection;
using Fanstatic.Commands;
using Fanstatic.Commands.Build;
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
    public void Render_ShouldKeepCaseDistinctTemplatePathsDistinct()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var site = CreateSite(TestSitePathConst06);
        var page = GetPage(site, "/index.html");
        var engine = new FluidTemplateEngine();
        engine.Initialize(site);
        var themePath = CreateTemporaryTheme(("single.html", "lower"), ("Single.html", "upper"));

        try
        {
            Assert.Equal("lower", engine.Render(Path.Combine(themePath, "single.html"), site, page));
            Assert.Equal("upper", engine.Render(Path.Combine(themePath, "Single.html"), site, page));
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
    public void Render_ShouldCollectTemplateMetricsWhenEnabled()
    {
        var site = CreateSite(TestSitePathConst06, templateMetrics: true);
        Assert.True(site.Options.TemplateMetrics);
        var page = GetPage(site, "/index.html");
        var engine = new FluidTemplateEngine();
        engine.Initialize(site);
        var themePath = CreateTemporaryTheme(("single.html", "file body"));
        var templatePath = Path.Combine(themePath, "single.html");

        try
        {
            Assert.Equal("file body", engine.Render(templatePath, site, page));
            Assert.Equal("file body", engine.Render(templatePath, site, page));
            Assert.Equal("inline body", engine.RenderInline("inline body", site, page));
            Assert.Equal("inline body", engine.RenderInline("inline body", site, page));

            var metrics = engine.GetTemplateMetrics().ToDictionary(metric => metric.TemplatePath);
            Assert.True(metrics.TryGetValue(templatePath.Replace('\\', '/'), out var fileMetric),
                $"Collected metrics: {string.Join(", ", metrics.Keys)}");
            Assert.NotNull(fileMetric);
            Assert.Equal(2, fileMetric.CallCount);
            Assert.Equal(1, fileMetric.CacheHits);
            Assert.True(fileMetric.TotalTime >= fileMetric.MaximumTime);
            Assert.True(fileMetric.MaximumTime >= TimeSpan.Zero);

            var inlineMetric = Assert.IsType<TemplateMetric>(metrics["(inline template)"]);
            Assert.Equal(2, inlineMetric.CallCount);
            Assert.Equal(1, inlineMetric.CacheHits);
            Assert.True(inlineMetric.TotalTime >= inlineMetric.MaximumTime);
        }
        finally
        {
            Directory.Delete(themePath, recursive: true);
        }
    }

    [Fact]
    public void Render_ShouldNotCollectTemplateMetricsWhenDisabled()
    {
        var site = CreateSite(TestSitePathConst06);
        var page = GetPage(site, "/index.html");
        var engine = new FluidTemplateEngine();
        engine.Initialize(site);

        Assert.Equal("inline body", engine.RenderInline("inline body", site, page));
        Assert.Empty(engine.GetTemplateMetrics());
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

    Site CreateSite(string testSitePath, bool templateMetrics = false)
    {
        var options = new BuildOptions
        {
            SourceArgument = Path.GetFullPath(Path.Combine(TestSitesPath, testSitePath)),
            Output = string.Empty,
            TemplateMetrics = templateMetrics,
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
