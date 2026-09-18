using System.Collections.Frozen;
using Fanstatic.Models;

namespace Fanstatic.Helpers;

/// <summary>
/// Helper methods for scanning files.
/// </summary>
public static class FileUtils
{
    /// <summary>
    /// Gets the content of a template file based on the page and the theme path.
    /// The cache stores the resolved template path (not the file content).
    /// </summary>
    /// <param name="site"></param>
    /// <param name="page">The page to determine the template index.</param>
    /// <param name="isBaseTemplate">Indicates whether the template is a base template.</param>
    /// <returns>The content of the template file, or empty string when not found.</returns>
    public static string GetTemplate(this Site site, Page page, bool isBaseTemplate = false)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(site);

        var templatePath = GetTemplatePath(site, page, isBaseTemplate);
        return string.IsNullOrEmpty(templatePath) ? string.Empty : File.ReadAllText(templatePath);
    }

    /// <summary>
    /// Gets the resolved template file path based on the page and the theme path.
    /// The resolved path is cached for future lookups.
    /// </summary>
    /// <param name="site"></param>
    /// <param name="page">The page to determine the template index.</param>
    /// <param name="isBaseTemplate">Indicates whether the template is a base template.</param>
    /// <returns>The resolved template file path, or empty string when not found.</returns>
    public static string GetTemplatePath(this Site site, Page page, bool isBaseTemplate = false)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(site);

        CacheTemplateIndex index = new(page.Section, page.Kind, page.Type, page.OutputFormat);

        var cache = isBaseTemplate
            ? site.CacheManager.BaseTemplateCache
            : site.CacheManager.ContentTemplateCache;

        if (cache.TryGetValue(index, out var cachedPath))
        {
            return cachedPath;
        }

        var templatePaths = page.GetTemplateLookupOrder(isBaseTemplate);
        var resolvedPath = ResolveTemplatePath(templatePaths, site.SourceThemePath);

        lock (cache)
        {
            _ = cache.TryAdd(index, resolvedPath);
        }

        return resolvedPath;
    }

    /// <summary>
    /// Resolves the first existing template file path from the lookup order.
    /// </summary>
    /// <param name="templatePaths">Relative template lookup paths.</param>
    /// <param name="themePath">Theme root path.</param>
    /// <returns>The resolved full path, or empty string when not found.</returns>
    static string ResolveTemplatePath(IEnumerable<string> templatePaths, string themePath)
    {
        ArgumentNullException.ThrowIfNull(templatePaths);

        foreach (var templatePath in templatePaths
                     .Select(templatePath => Path.Combine(themePath, templatePath))
                     .Where(File.Exists))
        {
            return templatePath;
        }

        return string.Empty;
    }

    /// <summary>
    /// Gets the lookup order for template files based on the theme path, page, and template type.
    /// </summary>
    /// <param name="page">The page to determine the template index.</param>
    /// <param name="isBaseTemplate">Indicates whether the template is a base template.</param>
    /// <returns>The list of template paths in the lookup order.</returns>
    public static IEnumerable<string> GetTemplateLookupOrder(this Page page, bool isBaseTemplate)
    {
        ArgumentNullException.ThrowIfNull(page);

        string[] sections = page.Section is null ? [string.Empty] : [page.Section, string.Empty];
        string[] types = page.Type is null ? [string.Empty, "_default"] : [page.Type, string.Empty, "_default"];
        string[] outputFormats = ["." + page.OutputFormatObj.Extension, string.Empty];

        var formatName = page.OutputFormat;
        var formatExt = "." + page.OutputFormatObj.Extension;
        var formatNameDiffersFromExt =
            !formatName.Equals(page.OutputFormatObj.Extension, StringComparison.OrdinalIgnoreCase);

        // Format-name-specific paths come first (e.g. "_default/sitemap.xml" before "_default/list.xml")
        var formatSpecificPaths = formatNameDiffersFromExt
            ? types.SelectMany(type => new[]
            {
                Path.Combine(type, formatName + formatExt),
                Path.Combine(type, formatName)
            })
            : [];

        var kinds = isBaseTemplate ? GetAllKindsBase(page.Kind) : GetAllKinds(page.Kind);

        var genericPaths = sections
            .SelectMany(section => types.Select(type => new { section, type }))
            .SelectMany(x => kinds.Select(kind => new { x.section, x.type, kind }))
            .SelectMany(x => outputFormats.Select(outputFormat => new { x.section, x.type, x.kind, outputFormat }))
            .Select(x => Path.Combine(x.section, x.type, x.kind) + x.outputFormat);

        return formatSpecificPaths.Concat(genericPaths).Distinct();
    }

    static readonly FrozenDictionary<Kind, string[]> KindLookup =
        Enum.GetValues<Kind>()
            .ToFrozenDictionary(
                selectedKind => selectedKind,
                selectedKind => Enum.GetValues<Kind>()
                    .Where(kind => selectedKind.HasFlag(kind))
                    .OrderByDescending(kind => kind)
                    .Select(kind => kind.ToString())
                    .ToArray());

    static readonly FrozenDictionary<Kind, string[]> KindBaseLookup =
        KindLookup.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value
                .Select(kindSelected => kindSelected + "-baseof")
                .Append("baseof")
                .ToArray());

    static IEnumerable<string> GetAllKinds(Kind kind) => KindLookup[kind];

    static IEnumerable<string> GetAllKindsBase(Kind kind) => KindBaseLookup[kind];

    /// <summary>
    /// Default Output Formats.
    /// </summary>
    public static readonly FrozenDictionary<string, OutputFormat> OutputFormats =
        new Dictionary<string, OutputFormat>
        {
            {
                "html", new OutputFormat
                {
                    Extension = "html"
                }
            },
            {
                "rss", new OutputFormat
                {
                    Extension = "xml",
                    NoUgly = true
                }
            },
            {
                "robots",
                new OutputFormat
                {
                    BaseName = "robots",
                    Extension = "xml",
                    NoUgly = true
                }
            },
            {
                "sitemap",
                new OutputFormat
                {
                    BaseName = "sitemap",
                    Extension = "xml",
                    NoUgly = true
                }
            }
        }.ToFrozenDictionary();

    static readonly FrozenDictionary<string, string> BuiltinTemplates =
        new Dictionary<string, string>
        {
            {
                "sitemap", """
                           <?xml version="1.0" encoding="UTF-8"?>
                           <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                           {%- for p in site.AllRegularPages %}
                             <url>
                               <loc>{{ p.Permalink }}</loc>
                               {%- if p.LastMod %}
                               <lastmod>{{ p.LastMod | date: "%Y-%m-%dT%H:%M:%SZ" }}</lastmod>
                               {%- endif %}
                             </url>
                           {%- endfor %}
                           </urlset>
                           """
            }
        }.ToFrozenDictionary();

    /// <summary>
    /// Returns the built-in inline template for the given output format, or empty string when none.
    /// </summary>
    public static string GetBuiltinTemplate(Page page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return BuiltinTemplates.TryGetValue(page.OutputFormat, out var template)
            ? template
            : string.Empty;
    }
}
