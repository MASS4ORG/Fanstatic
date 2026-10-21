using Fanstatic.Models;

namespace Fanstatic.Helpers;

/// <summary>
/// The site and page a wikilink is resolved against, together with the missing-target policy.
/// </summary>
/// <param name="site">The site holding the pages and taxonomies.</param>
/// <param name="page">The page being rendered, whose language and output format are the defaults.</param>
public sealed class WikiLinkScope(ISite site, IPage page)
{
    /// <summary>
    /// The site holding the pages and taxonomies.
    /// </summary>
    public ISite Site { get; } = site;

    /// <summary>
    /// The page being rendered.
    /// </summary>
    public IPage Page { get; } = page;
}
