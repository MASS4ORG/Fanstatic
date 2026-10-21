namespace Fanstatic.Helpers;

/// <summary>
/// Ambient context available to Markdig inline parsers and renderers while a page's Markdown is converted.
/// The pipeline is shared across the site, so the calling site and page are supplied per render through an
/// <see cref="AsyncLocal{T}"/> instead of being baked into the parser instances.
/// </summary>
public static class WikiLinkContext
{
    static readonly AsyncLocal<WikiLinkScope?> Current = new();

    /// <summary>
    /// The active scope, or null when no page is being rendered.
    /// </summary>
    public static WikiLinkScope? CurrentScope => Current.Value;

    /// <summary>
    /// Runs <paramref name="render"/> with the given site and page available to wikilink resolution.
    /// </summary>
    public static T Run<T>(Models.ISite site, Models.IPage page, Func<T> render)
    {
        ArgumentNullException.ThrowIfNull(render);

        var previous = Current.Value;
        Current.Value = new WikiLinkScope(site, page);
        try
        {
            return render();
        }
        finally
        {
            Current.Value = previous;
        }
    }
}
