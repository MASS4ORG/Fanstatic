namespace Fanstatic.Models;

/// <summary>
/// Page resources. All files that accompany a page.
/// </summary>
public class Resource : IResource
{
    /// <inheritdoc/>
    public required ISiteOutput Site { get; init; }

    /// <inheritdoc/>
    public string? Title { get; set; }

    /// <inheritdoc/>
    public required string SourceRelativePath { get; init; }

    #region IOutput

    /// <inheritdoc/>
    public Uri RelPermalink { get; set; } = new(".", UriKind.RelativeOrAbsolute);

    #endregion IOutput

    #region IParams

    /// <inheritdoc/>
    public Dictionary<string, object> Params { get; set; } = [];

    #endregion IParams
}
