using Fanstatic.Helpers;

namespace Fanstatic.Models;

/// <summary>
/// Raw resource representation for ContentSource
/// </summary>
public class ContentSourceResource
{
    /// <summary>
    /// Relative path of the resource, always with <c>/</c> separators
    /// </summary>
    public required string SourceRelativePath { get; init => field = UrlExtension.NormalizeToUnix(value); }

    /// <summary>
    /// Additional parameters for the resource
    /// </summary>
    public Dictionary<string, object> Params { get; set; } = [];

    /// <summary>
    /// The final resource object from this
    /// </summary>
    public Resource? Resource { get; internal set; }
}
