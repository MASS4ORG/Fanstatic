namespace Fanstatic.Models;

/// <summary>
/// Structured heading information collected from a page's Markdown.
/// </summary>
public sealed class PageFragments
{
    /// <summary>
    /// Initializes the fragments from the page's headings.
    /// </summary>
    public PageFragments(IReadOnlyList<Heading> headings)
    {
        Headings = headings;
        Identifiers = headings
            .Select(static heading => heading.Id)
            .Where(static id => id.Length != 0)
            .ToList();
    }

    /// <summary>
    /// Headings in document order.
    /// </summary>
    public IReadOnlyList<Heading> Headings { get; }

    /// <summary>
    /// Identifiers of the headings in document order.
    /// </summary>
    public IReadOnlyList<string> Identifiers { get; }
}
