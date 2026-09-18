using CommandLine;
using Fanstatic.Models;

namespace Fanstatic.Commands.API;

/// <summary>
/// Options for generating API documentation.
/// </summary>
[Verb("api", HelpText = "Generate API pages from a C# project.")]
public class ApiGeneratorOptions : GenerateOptions
{
    /// <summary>
    /// Paths to the source C# projects.
    /// </summary>
    [Option('p', "project", Required = true,
        HelpText = "Path(s) to the source C# project(s). Multiple paths can be separated by semicolons.")]
    public IEnumerable<string> SourceProjects { get; init; } = new[] { "./" };

    /// <summary>
    /// Relative output path for API documentation.
    /// </summary>
    [Option('o', "output", Required = false, HelpText = "Relative output path for API documentation.")]
    public string Output { get; init; } = "api";

    /// <summary>
    /// Specifies how to handle an existing output directory.
    /// </summary>
    [Option("output-policy", Required = false, HelpText = "Fail if output directory already exists.")]
    public OutputPolicy OutputPolicy { get; init; } = OutputPolicy.delete;

    /// <summary>
    /// Filter which assemblies or namespaces to include in the API documentation.
    /// </summary>
    [Option("filter", Required = false, HelpText = "Filter which assemblies or namespaces to include.")]
    public string? Filter { get; init; }

    /// <summary>
    /// Include private/internal members in the API documentation.
    /// </summary>
    [Option("include-private", Required = false, HelpText = "Include private/internal members.")]
    public bool IncludePrivate { get; init; }

    /// <summary>
    /// External repository link for source code linking (e.g., github.com/user/repo).
    /// </summary>
    [Option('e', "external-link", Required = false,
        HelpText = "External repository link for source code linking (e.g., github.com/user/repo).")]
    public string? ExternalLink { get; init; }
}
