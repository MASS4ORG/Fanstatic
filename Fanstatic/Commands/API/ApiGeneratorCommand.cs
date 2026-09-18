using Serilog;
using Fanstatic.Commands.API.APIModels;
using Fanstatic.Helpers;

namespace Fanstatic.Commands.API;

/// <summary>
/// Check links of a given site.
/// </summary>
public sealed class ApiGeneratorCommand : BaseGeneratorCommand
{
    ILogger _logger = null!;
    static CodeAnalyzer? _analyzer;
    static DocumentationGenerator? _generator;
    readonly ApiGeneratorOptions _options = null!;

    /// <inheritdoc />
    public ApiGeneratorCommand(ApiGeneratorOptions options, ILogger logger, IFileSystem fileSystem)
        : base(options, logger, fileSystem)
    {
        _options = options;
        _logger = logger;
        _analyzer = new(_logger);
    }

    // For NewSiteCommand:
    /// <summary>
    /// Creates and executes a new NewSiteCommand instance
    /// </summary>
    /// <param name="options">The new site options</param>
    /// <param name="logger">The logger instance</param>
    /// <param name="fileSystem"></param>
    /// <returns>A task with the result code (0 for success, 1 for failure)</returns>
    public static Task<int> Create(ApiGeneratorOptions options, ILogger logger, IFileSystem fileSystem)
    {
        var command = Run(options, logger, fileSystem);
        return Task.FromResult(command.Run().Result);
    }

    /// <summary>
    /// Creates a new instance of NewSiteCommand with the specified options, logger, and file system.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    /// <param name="fileSystem"></param>
    /// <returns></returns>
    public static ApiGeneratorCommand Run(ApiGeneratorOptions options, ILogger logger, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new ApiGeneratorCommand(options, logger, fileSystem);
    }

    /// <summary>
    /// Run the app
    /// </summary>
    /// <returns></returns>
    public async Task<int> Run()
    {
        _logger.Information("Starting API generation...");

        var structure = await ScanProjectsAsync(_options.SourceProjects);

        var outputDir = Path.Combine(Site.SourceContentPath, _options.Output);
        _logger.Debug("********* Output directory: {outputDir}", outputDir);

        try
        {
            _generator = new DocumentationGenerator(outputDir, _options.OutputPolicy, _logger);
            await _generator.GenerateDocumentationAsync(structure, _options);

            Summary(structure);
            _logger.Information("Scanning completed.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.Information($"Error generating documentation: {ex.Message}");
        }

        return 0;
    }

    async Task<ProjectStructure> ScanProjectsAsync(IEnumerable<string> projectPaths)
    {
        var combinedStructure = new ProjectStructure();

        foreach (var projectPath in projectPaths)
        {
            _logger.Information($"Starting analysis of project: {projectPath}");

            // Analyze the code structure
            var structure = await _analyzer!.AnalyzeProjectAsync(projectPath);

            // Merge the structures
            combinedStructure.AllClasses.AddRange(structure.AllClasses);
            foreach (var kvp in structure.NamespaceClasses)
            {
                if (!combinedStructure.NamespaceClasses.ContainsKey(kvp.Key))
                {
                    combinedStructure.NamespaceClasses[kvp.Key] = new List<ClassInfo>();
                }

                combinedStructure.NamespaceClasses[kvp.Key].AddRange(kvp.Value);
            }
        }

        return combinedStructure;
    }

    void Summary(ProjectStructure structure)
    {
        // Group partial classes to get accurate counts
        var groupedClasses = PartialClassMerger.GroupPartialClasses(structure.AllClasses);
        var totalMethods = groupedClasses.Sum(c => c.PublicMethods.Count);

        _logger.Information($"Analysis complete! Found:");
        _logger.Information($"- {structure.AllClasses.Count} total type declarations");
        _logger.Information($"- {groupedClasses.Count} unique types (after grouping partials)");
        _logger.Information($"- {structure.NamespaceClasses.Count} namespaces");
        _logger.Information($"- {totalMethods} public methods");

        if (!string.IsNullOrEmpty(_options.ExternalLink))
        {
            _logger.Information($"- External link: {_options.ExternalLink}");
        }
    }
}
