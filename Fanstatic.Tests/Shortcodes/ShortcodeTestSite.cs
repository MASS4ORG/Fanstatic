using Fanstatic.Commands.Build;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.Parsers;
using Serilog;

namespace Fanstatic.Test.Shortcodes;

/// <summary>
/// A site written to a temporary folder for shortcode tests, with a minimal <c>test</c> theme.
/// </summary>
sealed class ShortcodeTestSite : IDisposable
{
    /// <summary>
    /// Writes the files, adding a default configuration and layouts, and builds the site.
    /// </summary>
    public ShortcodeTestSite(ILogger logger, IReadOnlyDictionary<string, string> files)
    {
        Source = Path.Combine(Path.GetTempPath(), $"fanstatic-shortcodes-{Guid.NewGuid():N}");
        var all = new Dictionary<string, string>(files)
        {
            ["fanstatic.yaml"] = files.GetValueOrDefault("fanstatic.yaml")
                                 ?? "Title: test\nTheme: test\nbaseURL: https://example.com/\n",
            ["themes/test/_default/single.html"] = files.GetValueOrDefault("themes/test/_default/single.html")
                                                   ?? "{{ page.ContentPreRendered }}"
        };
        foreach (var (path, content) in all)
        {
            var fullPath = Path.Combine(Source, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        var options = new BuildOptions { SourceArgument = Source, Output = Path.Combine(Source, "public") };
        Site = SiteHelper.Init("fanstatic.yaml", options, new YamlParser(), logger, new StopwatchReporter(logger),
            new FileSystem());
    }

    /// <summary>
    /// The site folder.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// The built site.
    /// </summary>
    public Site Site { get; }

    /// <summary>
    /// The first page of a content file in an output format.
    /// </summary>
    public Page Page(string sourceRelativePath, string outputFormat = "html") =>
        Site.OutputReferences.Values
            .OfType<Page>()
            .First(page => page.SourceRelativePath == sourceRelativePath
                           && page.OutputFormat == outputFormat
                           && page.PageIndex == 1);

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(Source, recursive: true);
}
