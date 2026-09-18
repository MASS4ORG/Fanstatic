namespace Fanstatic.Models;

/// <summary>
/// The main configuration of the program, extracted from the app.yaml file.
/// </summary>
public interface ISiteSettings : IParams
{
    /// <summary>
    /// Site Title/Name.
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Site description
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Copyright information
    /// </summary>
    string? Copyright { get; }

    /// <summary>
    /// The base URL that will be used to build public links.
    /// </summary>
    Uri BaseUrl { get; set; }

    /// <summary>
    /// The appearance of a URL is either ugly or pretty.
    /// </summary>
    bool UglyUrLs { get; }

    /// <summary>
    /// The output format for each content kind
    /// </summary>
    Dictionary<Kind, List<string>> KindOutputFormats { get; }

    /// <summary>
    /// Number of regular pages shown per paginated page.
    /// </summary>
    int Paginate { get; }

    /// <summary>
    /// URL path segment used for paginated pages.
    /// </summary>
    string PaginatePath { get; }

    /// <summary>
    /// The languages configured for the site, keyed by their language code.
    /// </summary>
    Dictionary<string, LanguageSettings> Languages { get; }

    /// <summary>
    /// The default content language code. Used for content files without a
    /// language suffix and as the fallback for template helpers.
    /// </summary>
    string DefaultLanguage { get; }

    /// <summary>
    /// When true, content in the default language also gets a language prefix
    /// in its URL (e.g. <c>/en/about/</c>). When false (default) the default
    /// language has no URL prefix.
    /// </summary>
    bool DefaultContentLanguageInSubdir { get; }
}
