using CommandLine;

namespace Fanstatic.Commands.Serve;

/// <summary>
/// Command line options for the serve command.
/// </summary>
[Verb("serve", HelpText = "Starts the server")]
public class ServeOptions : GenerateOptions;
