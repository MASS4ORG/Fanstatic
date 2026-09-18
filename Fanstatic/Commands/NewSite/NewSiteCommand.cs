using Serilog;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.Parsers;

namespace Fanstatic.Commands.NewSite;

/// <summary>
/// Check links of a given site.
/// </summary>
public sealed class NewSiteCommand(NewSiteOptions options, ILogger logger, IFileSystem fileSystem, ISite site)
{
    static SiteSettings _siteSettings = null!;

    // For NewSiteCommand:
    /// <summary>
    /// Creates and executes a new NewSiteCommand instance
    /// </summary>
    /// <param name="options">The new site options</param>
    /// <param name="logger">The logger instance</param>
    /// <returns>A task with the result code (0 for success, 1 for failure)</returns>
    public static Task<int> Create(NewSiteOptions options, ILogger logger)
    {
        var fileSystem = new FileSystem();
        var command = Run(options, logger, fileSystem);
        return Task.FromResult(command.Run());
    }

    /// <summary>
    /// Generate the needed data for the class
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    /// <param name="fileSystem"></param>
    /// <returns></returns>
    public static NewSiteCommand Run(NewSiteOptions options, ILogger logger, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(options);

        _siteSettings = new SiteSettings
        {
            Title = options.Title,
            Description = options.Description,
            BaseUrl = options.BaseUrl
        };

        var site = new Site(
            new GenerateOptions
            {
                SourceOption = options.Output
            },
            _siteSettings, new YamlParser(), null!, null);
        return new NewSiteCommand(options, logger, fileSystem, site);
    }

    /// <summary>
    /// Run the app
    /// </summary>
    /// <returns></returns>
    public int Run()
    {
        var outputPath = Path.GetFullPath(options.Output);
        var siteSettingsPath = Path.Combine(outputPath, "fanstatic.yaml");

        if (fileSystem.FileExists(siteSettingsPath) && !options.Force)
        {
            logger.Error("{directoryPath} already exists", outputPath);
            return 1;
        }

        logger.Information("Creating a new site: {title} at {outputPath}", site.Title, outputPath);

        try
        {
            CreateFolders(site.SourceFolders);
            site.Parser.SerializeAndSave(_siteSettings, siteSettingsPath);
        }
        catch (Exception ex)
        {
            logger.Error("Failed to export site settings: {ex}", ex);
            return 1;
        }

        logger.Information("Done");
        return 0;
    }

    /// <summary>
    /// Create the standard folders
    /// </summary>
    /// <param name="folders"></param>
    void CreateFolders(IEnumerable<string> folders)
    {
        foreach (var folder in folders)
        {
            logger.Information("Creating {folder}", folder);
            fileSystem.DirectoryCreateDirectory(folder);
        }
    }
}
