using System.Text;
using Serilog;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.Parsers;

namespace Fanstatic.Commands;

/// <summary>
/// Base class for build and serve commands.
/// </summary>
public abstract class BaseGeneratorCommand
{
    /// <summary>
    /// The site configuration.
    /// </summary>
    public ISite Site { get; set; }

    /// <summary>
    /// The configuration file name.
    /// </summary>
    protected const string ConfigFile = "fanstatic.yaml";

    /// <summary>
    /// The front matter parser instance. The default is YAML.
    /// </summary>
    protected IFrontMatterParser Parser { get; } = new YamlParser();

    /// <summary>
    /// The stopwatch reporter.
    /// </summary>
    protected StopwatchReporter Stopwatch { get; }

    /// <summary>
    /// The logger (Serilog).
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// File system functions (file and directory)
    /// </summary>
    protected readonly IFileSystem Fs;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseGeneratorCommand"/> class.
    /// </summary>
    /// <param name="options">The generate options.</param>
    /// <param name="logger">The logger instance. Injectable for testing</param>
    /// <param name="fs"></param>
    /// <param name="site"></param>
    protected BaseGeneratorCommand(IGenerateOptions options, ILogger logger, IFileSystem fs, ISite? site = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        Logger = logger;
        Stopwatch = new(logger);
        Fs = fs;

        logger.Information("Source path: {source}", propertyValue: options.Source);

        if (site == null)
        {
            Site = SiteHelper.Init(ConfigFile, options, Parser, logger, Stopwatch, fs);
        }
        else
        {
            Site = site;
            Site.ProcessPages();
            Site.RegisterPaginatedUrls();
        }
    }

    /// <summary>
    /// Reports all created output pages in debug log
    /// </summary>
    protected void ReportAllCreatedOutput()
    {
        StringBuilder report = new("----------------- All outputs\n");
        var uris = Site.OutputReferences.Keys.OrderBy(uri => uri.OriginalString);
        foreach (var page in uris)
        {
            report.AppendLine(page.OriginalString);
        }

        Logger.Debug(report.ToString());
    }
}
