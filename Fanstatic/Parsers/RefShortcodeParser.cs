using Fanstatic.Helpers;
using Fanstatic.Models;

namespace Fanstatic.Parsers;

/// <summary>
/// Resolves the targets of the <c>ref</c> and <c>relref</c> built-in shortcodes into permalinks.
/// <c>ref</c> resolves to the absolute permalink, <c>relref</c> to the relative one. The target
/// page is looked up using the calling page's output format and language, unless overridden with
/// the <c>outputFormat</c> and <c>lang</c> named arguments.
/// </summary>
/// Usage: <c>{{&lt; ref "posts/my-post.md" &gt;}}</c>,
/// <c>{{&lt; relref path="/posts/my-post.md" lang="pt-br" outputFormat="rss" &gt;}}</c>.
public static class RefShortcodeParser
{
    /// <summary>
    /// Returns the permalink of the page at <paramref name="path"/>, with its anchor, or <c>#</c> when no page matches.
    /// </summary>
    /// <param name="site">The site holding the pages.</param>
    /// <param name="page">The calling page, whose language, output format and directory are the defaults.</param>
    /// <param name="path">Content path of the target, optionally with a <c>#anchor</c>.</param>
    /// <param name="lang">Language of the target, null for the calling page's language.</param>
    /// <param name="outputFormat">Output format of the target, null for the calling page's format.</param>
    /// <param name="isAbsolute">True for the absolute permalink, false for the relative one.</param>
    public static string Resolve(ISite site, IPage page, string? path, string? lang, string? outputFormat,
        bool isAbsolute)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(page);

        if (string.IsNullOrWhiteSpace(path))
        {
            site.Logger.Error("ref/relref shortcode with no path found in {File}", page.SourceRelativePath);
            return "#";
        }

        var (targetPath, anchor) = SplitAnchor(path);
        var target = FindPage(site, page, targetPath, lang ?? page.ContentSource.Language,
            outputFormat ?? page.OutputFormat);

        if (target is null)
        {
            site.Logger.Error("ref/relref: unable to resolve {Path} referenced in {File}", path,
                page.SourceFullPath(site.SourceContentPath));
            return "#";
        }

        var url = (isAbsolute ? target.Permalink : target.RelPermalink).ToString();
        return string.IsNullOrEmpty(anchor) ? url : $"{url}#{anchor}";
    }

    static (string path, string anchor) SplitAnchor(string path)
    {
        var hashIndex = path.IndexOf('#');
        return hashIndex < 0 ? (path, string.Empty) : (path[..hashIndex], path[(hashIndex + 1)..]);
    }

    /// <summary>
    /// Looks up a page by content path, first among the exact language/outputFormat requested,
    /// falling back to the default-language variant, then any other matching variant, when a translation is missing.
    /// </summary>
    static IPage? FindPage(ISite site, IPage callerPage, string targetPath, string lang,
        string outputFormat)
    {
        var normalizedTarget = Normalize(targetPath);
        var isRootRelative = targetPath.TrimStart().StartsWith('/');
        var pageRelativeTarget = isRootRelative
            ? null
            : Normalize(Combine(callerPage.SourceRelativePathDirectory, targetPath));

        IPage? fallback = null;
        foreach (var candidate in site.Pages)
        {
            if (candidate is Page { PageIndex: > 1 })
            {
                continue;
            }

            var logical = LogicalPath(candidate);
            var isMatch = string.Equals(logical, normalizedTarget, StringComparison.OrdinalIgnoreCase)
                          || (pageRelativeTarget is not null
                              && string.Equals(logical, pageRelativeTarget, StringComparison.OrdinalIgnoreCase));

            if (!isMatch)
            {
                continue;
            }

            if (string.Equals(candidate.ContentSource.Language, lang, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.OutputFormat, outputFormat, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            if (fallback is null || (!IsDefaultLanguageVariant(fallback) && IsDefaultLanguageVariant(candidate)))
            {
                fallback = candidate;
            }
        }

        return fallback;

        bool IsDefaultLanguageVariant(IPage candidate) =>
            string.Equals(candidate.ContentSource.Language, site.DefaultLanguageObj.Code,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.OutputFormat, outputFormat, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The content path of a page relative to the content root, without extension or
    /// language suffix, and with a bundle's <c>index</c>/<c>_index</c> filename collapsed
    /// into its directory (mirrors how <c>ref</c> paths are typically written).
    /// </summary>
    static string LogicalPath(IPage page)
    {
        var dir = UrlExtension.NormalizeToUnix(page.SourceRelativePathDirectory).Trim('/');
        var name = page.SourceFileNameWithoutExtension ?? string.Empty;
        return name is "index" or "_index" ? dir : string.IsNullOrEmpty(dir) ? name : $"{dir}/{name}";
    }

    static string Combine(string directory, string relativePath)
    {
        var dir = UrlExtension.NormalizeToUnix(directory).Trim('/');
        var rel = relativePath.Trim().TrimStart('/');
        return string.IsNullOrEmpty(dir) ? rel : $"{dir}/{rel}";
    }

    static string Normalize(string path)
    {
        var normalized = UrlExtension.NormalizeToUnix(path).Trim().Trim('/');

        if (normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^3];
        }

        if (normalized.EndsWith("/index", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^"/index".Length];
        }
        else if (normalized.EndsWith("/_index", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^"/_index".Length];
        }
        else if (normalized.Equals("index", StringComparison.OrdinalIgnoreCase)
                 || normalized.Equals("_index", StringComparison.OrdinalIgnoreCase))
        {
            normalized = string.Empty;
        }

        return normalized;
    }
}
