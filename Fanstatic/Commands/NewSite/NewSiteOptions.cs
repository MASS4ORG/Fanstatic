using CommandLine;

namespace Fanstatic.Commands.NewSite;

/// <summary>
/// Command line options to generate a simple site from scratch.
/// </summary>
[Verb("new-site", HelpText = "Generate a simple site from scratch")]
public class NewSiteOptions
{
    /// <summary>
    /// The path of the output files.
    /// </summary>
    [Value(0)]
    public string Output { get; init; } = "./";

    /// <summary>
    /// Force site creation.
    /// </summary>
    [Option('f', "force", Required = false, HelpText = "Force site creation")]
    public bool Force { get; init; }

    /// <summary>
    /// Site title.
    /// </summary>
    [Option("title", Required = false, HelpText = "Site title")]
    public string Title { get; init; } = "My Site";

    /// <summary>
    /// Site description.
    /// </summary>
    [Option("description", Required = false, HelpText = "Site description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Site base url.
    /// </summary>
    [Option("url", Required = false, HelpText = "Site base url")]
    public Uri BaseUrl { get; init; } = new("https://example.org/");
}
