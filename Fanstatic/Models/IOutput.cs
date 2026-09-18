namespace Fanstatic.Models;

/// <summary>
/// Page or Resources (files) that will be considered as output.
/// </summary>
public interface IOutput
{
    /// <summary>
    /// The URL for the content.
    /// </summary>
    Uri Permalink => new(Site.BaseUrl, RelPermalink);

    /// <summary>
    /// The relative permalink's "path"
    /// </summary>
    Uri PermalinkDir =>
        new(Site.BaseUrl, string.Concat(Permalink.Segments[..^1]));

    /// <summary>
    /// The relative permalink's filename
    /// </summary>
    string PermalinkFilename => Path.GetFileName(Permalink.LocalPath);

    /// <summary>
    /// The URL for the content.
    /// </summary>
    Uri RelPermalink { get; set; }

    /// <summary>
    /// The relative permalink's "path"
    /// </summary>
    Uri RelPermalinkDir
    {
        get
        {
            var segments = RelPermalink.ToString().Split('/')[..^1];

            return new(segments.Length > 1 ? string.Join("/", segments) : "/"
                , UriKind.Relative);
        }
    }

    /// <summary>
    /// The relative permalink's filename
    /// </summary>
    string RelPermalinkFilename => Path.GetFileName(RelPermalink.ToString());

    /// <summary>
    /// Point to the site configuration.
    /// </summary>
    ISiteOutput Site { get; init; }
}
