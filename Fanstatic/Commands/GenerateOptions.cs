using CommandLine;
using JetBrains.Annotations;

namespace Fanstatic.Commands;

/// <summary>
/// Basic Command line options for the serve and build command.
/// </summary>
public class GenerateOptions : IGenerateOptions
{
    /// <inheritdoc/>
    [UsedImplicitly]
    [Option('v', "verbose", Required = false, HelpText = "Enable debug-level logging (default: information)")]
    public bool Verbose { get; init; }

    /// <inheritdoc/>
    public string Source => string.IsNullOrEmpty(SourceOption) ? SourceArgument : SourceOption;

    /// <inheritdoc/>
    [UsedImplicitly]
    [Option('d', "draft", Required = false, HelpText = "Include draft content")]
    public bool Draft { get; init; }

    /// <inheritdoc/>
    [UsedImplicitly]
    [Option('f', "future", Required = false, HelpText = "Include content with dates in the future")]
    public bool Future { get; init; }

    /// <inheritdoc/>
    [UsedImplicitly]
    [Option('e', "expired", Required = false, HelpText = "Include content with ExpiredDate dates from the past")]
    public bool Expired { get; init; }

    /// <summary>
    /// The path of the source files
    /// </summary>
    [UsedImplicitly]
    [Value(0)]
    public string SourceArgument { private get; init; } = "./";

    /// <summary>
    /// The path of the source files, as --source commandline option
    /// </summary>
    [UsedImplicitly]
    [Option('s', "source", Required = false, HelpText = "Source directory path")]
    public string SourceOption { private get; init; } = string.Empty;
}
