namespace Fanstatic.Models;

/// <summary>
/// A taxonomy term and the content pages assigned to it.
/// </summary>
public sealed class TaxonomyTerm(IPage page)
{
    /// <summary>
    /// The term's content path name.
    /// </summary>
    public string Name { get; } = page.SourcePathLastDirectory ?? page.Title ?? string.Empty;

    /// <summary>
    /// The term's display title.
    /// </summary>
    public string Title => Page.Title ?? Name;

    /// <summary>
    /// The term page's public URL.
    /// </summary>
    public Uri Permalink { get; } = page.Permalink;

    /// <summary>
    /// Content pages assigned to the term.
    /// </summary>
    public IReadOnlyList<IPage> Pages { get; } = page.RegularPages.ToList();

    /// <summary>
    /// The number of content pages assigned to the term.
    /// </summary>
    public int Count => Pages.Count;

    /// <summary>
    /// The underlying term page.
    /// </summary>
    public IPage Page { get; } = page;
}
