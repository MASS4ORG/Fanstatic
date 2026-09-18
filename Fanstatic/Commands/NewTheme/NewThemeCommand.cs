using Serilog;
using Fanstatic.Models;
using Fanstatic.Parsers;

namespace Fanstatic.Commands.NewTheme;

/// <summary>
/// Check links of a given site.
/// </summary>
public sealed class NewThemeCommand(NewThemeOptions options, ILogger logger)
{
    // For NewThemeCommand:
    /// <summary>
    /// Creates and executes a new NewThemeCommand instance
    /// </summary>
    /// <param name="options">The new theme options</param>
    /// <param name="logger">The logger instance</param>
    /// <returns>A task with the result code (0 for success, 1 for failure)</returns>
    public static Task<int> Create(NewThemeOptions options, ILogger logger)
    {
        var command = new NewThemeCommand(options, logger);
        return Task.FromResult(command.Run());
    }

    /// <summary>
    /// Run the app
    /// </summary>
    /// <returns></returns>
    public int Run()
    {
        var theme = new Theme
        {
            Title = options.Title,
            Path = options.Output
        };
        var outputPath = Path.GetFullPath(options.Output);
        var themePath = Path.Combine(outputPath, "fanstatic.yaml");

        if (File.Exists(themePath) && !options.Force)
        {
            logger.Error("{directoryPath} already exists", outputPath);
            return 1;
        }

        logger.Information("Creating a new site: {title} at {outputPath}", theme.Title, outputPath);

        CreateFolders(theme.Folders);

        try
        {
            new YamlParser().SerializeAndSave(theme, themePath);
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
            Directory.CreateDirectory(folder);
        }
    }
}
