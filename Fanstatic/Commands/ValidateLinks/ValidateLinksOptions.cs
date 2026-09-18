using CommandLine;

namespace Fanstatic.Commands.ValidateLinks;

/// <summary>
/// Command line options for the validate-links command.
/// </summary>
[Verb("validate-links", HelpText = "Validate internal and external links")]
public class ValidateLinksOptions : GenerateOptions
{
    /// <summary>
    /// Whether to check external links.
    /// </summary>
    [Option('x', "external", Required = false, HelpText = "Also check external links")]
    public bool CheckExternal { get; init; }

    /// <summary>
    /// List of links to ignore when checking.
    /// </summary>
    [Option('i', "ignore", Required = false, HelpText = "List of links to ignore checking")]
    public IEnumerable<string> Ignore { get; init; } = [];

    /// <summary>
    /// Whether to require an absolute link's domain to match the site's BaseUrl before
    /// treating it as internal. When disabled (the default), a link is still resolved
    /// against the site's own pages if its path matches one, even when its domain
    /// doesn't match BaseUrl (e.g. content hardcodes the production URL while validating
    /// locally). Enable this to force such mismatched-domain links to be validated as
    /// external requests against the BaseUrl domain instead.
    /// </summary>
    [Option('b', "strict-baseurl", Required = false,
        HelpText = "Only treat absolute links as internal when their domain matches BaseUrl; " +
                   "otherwise validate them as external requests")]
    public bool StrictBaseUrl { get; init; }
}
