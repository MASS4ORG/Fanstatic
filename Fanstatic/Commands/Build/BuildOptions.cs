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

    /// <summary>
    /// Exit successfully even when template errors were found.
    /// </summary>
    [Option("continue-on-error", Required = false,
        HelpText = "Exit with 0 even when template errors were found")]
    public bool ContinueOnError { get; set; }
}
