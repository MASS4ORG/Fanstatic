using System.Collections.Concurrent;
using Fanstatic.Models;

namespace Fanstatic.Helpers;

/// <summary>
/// Manages all the lists and dictionaries used for cache for the site
/// </summary>
public class SiteCacheManager
{
    /// <summary>
    /// Cache for content templates.
    /// </summary>
    public Dictionary<CacheTemplateIndex, string> ContentTemplateCache { get; } = [];

    /// <summary>
    /// Cache for base templates.
    /// </summary>
    public Dictionary<CacheTemplateIndex, string> BaseTemplateCache { get; } = [];

    /// <summary>
    /// Cache for tag page.
    /// </summary>
    public ConcurrentDictionary<string, ContentSource> AutomaticContentCache { get; } = new();

    /// <summary>
    /// Resets the template cache to force a reload of all templates.
    /// </summary>
    public void ResetCache()
    {
        BaseTemplateCache.Clear();
        ContentTemplateCache.Clear();
        AutomaticContentCache.Clear();
    }
}
