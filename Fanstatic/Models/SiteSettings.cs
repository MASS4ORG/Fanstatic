using YamlDotNet.Serialization;

namespace Fanstatic.Models;

/// <summary>
/// The main configuration of the program, extracted from the app.yaml file.
/// </summary>
[YamlSerializable]
public class SiteSettings : ISiteSettings
{
    #region ISiteSettings

    /// <inheritdoc/>
    public string Title { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string? Description { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string? Copyright { get; set; }

    /// <inheritdoc/>
    public Uri BaseUrl { get; set; } = new("http://localhost:2341",
        UriKind.RelativeOrAbsolute);

    /// <inheritdoc/>
    public Dictionary<Kind, List<string>> KindOutputFormats { get; set; } =
        new()
        {
            { Kind.home, ["html", "rss", "sitemap"] },
            { Kind.list, ["html", "rss"] },
            { Kind.section, ["html", "rss"] },
            { Kind.taxonomy, ["html", "rss"] },
            { Kind.term, ["html", "rss"] },
            { Kind.single, ["html", "rss"] },
            { Kind.rss, ["rss"] },
        };

    #endregion ISiteSettings

    /// <inheritdoc/>
    public Dictionary<string, LanguageSettings> Languages { get; set; } = [];

    /// <inheritdoc/>
    public string DefaultLanguage { get; set; } = "en";

    /// <inheritdoc/>
    public bool DefaultContentLanguageInSubdir { get; set; }

    /// <summary>
    /// Types of outputs
    /// </summary>
    public Dictionary<string, List<string>> Outputs { get; set; } = [];

    /// <summary>
    /// The global site theme.
    /// </summary>
    public string? Theme { get; set; }

    /// <summary>
    /// The theme folder where all themes are placed (if any).
    /// </summary>
    public string ThemeDir { get; set; } = "themes";

    /// <inheritdoc/>
    public bool UglyUrLs { get; set; }

    /// <summary>
    /// Number of items per pagination page.
    /// </summary>
    public int Paginate { get; set; } = 10;

    /// <summary>
    /// URL segment used for paginated pages.
    /// </summary>
    public string PaginatePath { get; set; } = "page";

    #region IParams

    /// <inheritdoc/>
    public Dictionary<string, object> Params { get; set; } = [];

    #endregion IParams
}
