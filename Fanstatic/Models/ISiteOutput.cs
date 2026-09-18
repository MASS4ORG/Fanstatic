using System.Collections.Concurrent;

namespace Fanstatic.Models;

public interface ISiteOutput
{
    /// <summary>
    /// Fanstatic internal variables
    /// </summary>
    FanstaticInfo Fanstatic { get; }

    /// <summary>
    /// Theme used.
    /// </summary>
    Theme? Theme { get; }

    /// <summary>
    /// The base URL that will be used to build public links.
    /// </summary>
    Uri BaseUrl { get; }

    /// <summary>
    /// The current language of this site output.
    /// </summary>
    LanguageSettings Language { get; }

    /// <summary>
    /// All languages configured for the site, sorted by weight.
    /// </summary>
    IReadOnlyList<LanguageSettings> Languages { get; }

    /// <summary>
    /// List of all pages, including generated, by their permalink.
    /// </summary>
    ConcurrentDictionary<Uri, IOutput> OutputReferences { get; }

    /// <summary>
    /// List of all pages, including generated.
    /// </summary>
    IEnumerable<IPage> Pages { get; }

    /// <summary>
    /// List of pages from the content folder.
    /// </summary>
    IEnumerable<IPage> RegularPages { get; }

    /// <summary>
    /// All regular pages across every output format. Intended for cross-format outputs such as sitemap.
    /// </summary>
    IEnumerable<IPage> AllRegularPages { get; }

    /// <summary>
    /// The page of the home page;
    /// </summary>
    IPage? Home { get; }
}
