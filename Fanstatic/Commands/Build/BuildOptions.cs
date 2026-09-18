using CommandLine;

namespace Fanstatic.Commands.Build;

/// <summary>
/// Command line options for the build command.
/// </summary>
[Verb("build", true, HelpText = "Builds the site")]
public class BuildOptions : GenerateOptions
{
    /// <summary>
    /// The path of the output files.
    /// </summary>
    [Option('o', "output", Required = false, HelpText = "Output directory path")]
    public required string Output { get; set; }
}
