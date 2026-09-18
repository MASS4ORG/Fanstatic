using Serilog;
using Fanstatic.Helpers;
using Fanstatic.Models;

namespace Fanstatic.Commands.Build;

/// <summary>
/// Build Command will build the site based on the source files.
/// </summary>
public class BuildCommand : BaseGeneratorCommand
{
    readonly BuildOptions _options;

    /// <summary>
    /// Entry point of the build command. It will be called by the main program
    /// in case the build command is invoked (which is by default).
    /// </summary>
    /// <param name="options">Command line options</param>
    /// <param name="logger">The logger instance. Injectable for testing</param>
    /// <param name="fs"></param>
    public BuildCommand(BuildOptions options, ILogger logger, IFileSystem fs)
        : base(options, logger, fs)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Run the command
    /// </summary>
    public int Run()
    {
        Logger.Information("Output path: {output}", _options.Output);

        // Generate the site pages
        CreateOutputFiles();

        // Copy theme static folder files into the root of the output folder
        if (Site.Theme is not null)
        {
            CopyFolder(Site.Theme.StaticFolder, _options.Output);
        }

        // Copy static folder files into the root of the output folder
        CopyFolder(Site.SourceStaticPath, _options.Output);

        // Generate the build report
        Stopwatch.LogReport(Site.Title);

        return 0;
    }

    /// <summary>
    /// Creates and executes a new BuildCommand instance
    /// </summary>
    /// <param name="options">The build options</param>
    /// <param name="logger">The logger instance</param>
    /// <returns>A task with the result code (0 for success, 1 for failure)</returns>
    public static Task<int> Create(BuildOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            var command = new BuildCommand(options, logger, new FileSystem());
            return Task.FromResult(command.Run());
        }
        catch (Exception ex)
        {
            logger.Error($"Build failed: {ex.Message}");
            return Task.FromResult(1);
        }
    }

    void CreateOutputFiles()
    {
        Stopwatch.Start("Create");

        var pagesCreated = 0;

        // Phase 1: snapshot original outputs (PageIndex == 1 or resources), then render.
        // Templates calling `| paginate:` register virtual pages as a side-effect.
        // Snapshotting avoids iterating the ConcurrentDictionary while it is being modified.
        var phase1 = Site.OutputReferences
            .Where(p => p.Value is not Page pg || pg.PageIndex == 1)
            .ToList();
        _ = Parallel.ForEach(phase1, pair => WriteOutput(pair, ref pagesCreated));

        // Phase 2: snapshot virtual pages registered during Phase 1, then render.
        var phase2 = Site.OutputReferences
            .Where(p => p.Value is Page pg && pg.PageIndex > 1)
            .ToList();
        _ = Parallel.ForEach(phase2, pair => WriteOutput(pair, ref pagesCreated));

        Stopwatch.Stop("Create", pagesCreated);
    }

    void WriteOutput(KeyValuePair<Uri, IOutput> pair, ref int pagesCreated)
    {
        var (path, output) = pair;

        if (output is IPage page)
        {
            var outputAbsolutePath = Path.Combine(_options.Output, path.ToString().TrimStart('/'));
            var outputDirectory = Path.GetDirectoryName(outputAbsolutePath);
            Fs.DirectoryCreateDirectory(outputDirectory!);

            var result = page.CompleteContent;
            Fs.FileWriteAllText(outputAbsolutePath, result);

            _ = Interlocked.Increment(ref pagesCreated);
            Logger.Debug("Page created {pagesCreated}: {Permalink}", pagesCreated, outputAbsolutePath);
        }
        else if (output is IResource resource)
        {
            var inputAbsolutePath = Path.Combine(Site.SourceContentPath, resource.SourceRelativePath);
            var outputAbsolutePath = Path.Combine(_options.Output, resource.RelPermalink.ToString().TrimStart('/'));
            var outputDirectory = Path.GetDirectoryName(outputAbsolutePath);
            Fs.DirectoryCreateDirectory(outputDirectory!);
            Fs.FileCopy(inputAbsolutePath, outputAbsolutePath, overwrite: true);
        }
    }

    /// <summary>
    /// Copy a folder content from source into the output folder.
    /// </summary>
    /// <param name="source">The source folder to copy from.</param>
    /// <param name="output">The output folder to copy to.</param>
    public void CopyFolder(string source, string output)
    {
        // Check if the source folder even exists
        if (!Fs.DirectoryExists(source))
        {
            return;
        }

        // Create the output folder if it doesn't exist
        Fs.DirectoryCreateDirectory(output);

        // Get all files in the source folder
        var files = Fs.DirectoryGetFiles(source);

        foreach (var fileFullPath in files)
        {
            // Get the filename from the full path
            var fileName = Path.GetFileName(fileFullPath);

            // Create the destination path by combining the output folder and the filename
            var destinationFullPath = Path.Combine(output, fileName);

            // Copy the file to the output folder
            Fs.FileCopy(fileFullPath, destinationFullPath, overwrite: true);
        }
    }
}
