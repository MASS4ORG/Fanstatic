using Fanstatic.Helpers;
using Fanstatic.Models;

namespace Fanstatic.Parsers;

/// <summary>
/// Resolves the targets of the <c>ref</c> and <c>relref</c> built-in shortcodes into permalinks.
/// Paths are tried caller-relative, then from the content root, then with the content directory
/// prefix removed, most specific first. Each path is tried against pages and page resources with
/// the requested language and output format, followed by progressively looser variants.
/// <c>ref</c> resolves to the absolute permalink, <c>relref</c> to the relative one. An anchor
/// without a path resolves to the calling page.
/// </summary>
/// Usage: <c>{{&lt; ref "posts/my-post.md" &gt;}}</c>,
/// <c>{{&lt; relref path="/content/posts/my-post.md" lang="pt-br" outputFormat="rss" &gt;}}</c>.
public static class RefShortcodeParser
{
    /// <summary>
    /// Looks up the page a content path refers to, using the calling page's directory for relative paths.
    /// Resources are ignored so that a nearer resource cannot hide a page at a wider path.
    /// </summary>
    /// <param name="site">The site holding the pages.</param>
    /// <param name="page">The calling page, whose language and output format are the defaults.</param>
    /// <param name="path">Content path of the target, without an anchor.</param>
    /// <param name="lang">Language of the target; null for the calling page's language.</param>
    /// <param name="outputFormat">Output format of the target; null for the calling page's format.</param>
    public static IPage? ResolvePage(ISite site, IPage page, string path, string? lang = null,
        string? outputFormat = null)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(page);

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Find(site, page, path, lang ?? page.ContentSource.Language,
            outputFormat ?? page.OutputFormat, false).Output as IPage;
    }

    /// <summary>
    /// Returns the permalink of the page or page resource at <paramref name="path"/>, with its anchor,
    /// or <c>#</c> when nothing matches.
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
        var target = targetPath.Trim().Length == 0
            ? page
            : Find(site, page, targetPath, lang ?? page.ContentSource.Language,
                outputFormat ?? page.OutputFormat, true).Output;

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
    /// Makes one pass over the pages and their resources, keeping the match with the most specific
    /// path, then language/format variant, with pages ahead of resources at the same level.
    /// </summary>
    static Match Find(ISite site, IPage callerPage, string path, string lang, string outputFormat,
        bool includeResources)
    {
        var (targetPath, _) = SplitAnchor(path);
        var candidates = PathCandidates(site, callerPage, targetPath);
        var pagePaths = candidates.Select(NormalizePagePath).ToArray();
        var resourcePaths = candidates.Select(NormalizePath).ToArray();
        var defaultLang = site.DefaultLanguageObj.Code;
        (string? Lang, string? Format)[] variants =
        [
            (lang, outputFormat),
            (defaultLang, outputFormat),
            (lang, null),
            (defaultLang, null),
            (null, null)
        ];

        var best = Match.Empty;
        foreach (var page in site.Pages.OfType<Page>())
        {
            if (page.PageIndex > 1)
            {
                continue;
            }

            var variant = VariantRank(page, variants);
            var pageMatch = MatchPage(page, pagePaths, variant);
            if (IsBetter(pageMatch, best))
            {
                best = pageMatch;
            }

            if (!includeResources || page.Resources is not { } resources)
            {
                continue;
            }

            var resourceMatch = MatchResources(resources, resourcePaths, variant);
            if (IsBetter(resourceMatch, best))
            {
                best = resourceMatch;
            }
        }

        return best;
    }

    static Match MatchPage(Page page, string[] paths, int variant)
    {
        var logicalPath = LogicalPath(page);
        var best = Match.Empty;
        for (var i = 0; i < paths.Length; i++)
        {
            if (string.Equals(logicalPath, paths[i], StringComparison.OrdinalIgnoreCase)
                && IsBetter(new Match(page, i, variant, 0), best))
            {
                best = new Match(page, i, variant, 0);
            }
        }

        return best;
    }

    static Match MatchResources(IEnumerable<IResource> resources, string[] paths, int variant)
    {
        var best = Match.Empty;
        foreach (var resource in resources)
        {
            var sourcePath = ResourcePath(resource);
            var permalinkPath = NormalizePath(resource.RelPermalink.ToString());
            for (var i = 0; i < paths.Length; i++)
            {
                var matches = string.Equals(sourcePath, paths[i], StringComparison.OrdinalIgnoreCase)
                              || string.Equals(permalinkPath, paths[i], StringComparison.OrdinalIgnoreCase);
                var match = new Match(resource, i, variant, 1);
                if (matches && IsBetter(match, best))
                {
                    best = match;
                }
            }
        }

        return best;
    }

    static bool IsBetter(Match candidate, Match current) =>
        candidate.Candidate < current.Candidate
        || candidate.Candidate == current.Candidate && candidate.Variant < current.Variant
        || candidate.Candidate == current.Candidate && candidate.Variant == current.Variant
           && candidate.Kind < current.Kind;

    /// <summary>
    /// Builds the ordered paths to try for a target: caller-relative first for non-root paths,
    /// then from the content root, then with the content directory prefix removed.
    /// </summary>
    static List<string> PathCandidates(ISite site, IPage callerPage, string path)
    {
        var trimmed = path.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        var isRootRelative = trimmed.StartsWith('/');
        var normalized = NormalizePath(trimmed);
        var contentDirectory = new DirectoryInfo(site.SourceContentPath).Name;
        var withoutContentDirectory = RemoveContentDirectory(normalized, contentDirectory);
        var candidates = new List<string>();

        if (!isRootRelative)
        {
            candidates.Add(Join(callerPage.SourceRelativePathDirectory, normalized));
        }

        candidates.Add(normalized);
        if (!string.Equals(withoutContentDirectory, normalized, StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(withoutContentDirectory);
        }

        return
        [
            .. candidates.Where(candidate => isRootRelative || candidate.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    static string RemoveContentDirectory(string path, string contentDirectory)
    {
        var prefix = contentDirectory + "/";
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? path[prefix.Length..] : path;
    }

    static int VariantRank(IPage page, (string? Lang, string? Format)[] variants)
    {
        for (var i = 0; i < variants.Length; i++)
        {
            if (MatchesVariant(page, variants[i].Lang, variants[i].Format))
            {
                return i;
            }
        }

        return -1;
    }

    static bool MatchesVariant(IPage page, string? lang, string? outputFormat) =>
        (lang is null || string.Equals(page.ContentSource.Language, lang, StringComparison.OrdinalIgnoreCase))
        && (outputFormat is null
            || string.Equals(page.OutputFormat, outputFormat, StringComparison.OrdinalIgnoreCase));

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

    static string ResourcePath(IResource resource) => NormalizePath(resource.SourceRelativePath);

    static string Join(string directory, string relativePath)
    {
        var dir = NormalizePath(directory);
        return string.IsNullOrEmpty(dir) ? relativePath : $"{dir}/{relativePath}";
    }

    static string NormalizePath(string path) => UrlExtension.NormalizeToUnix(path).Trim().Trim('/');

    static string NormalizePagePath(string path)
    {
        var normalized = NormalizePath(path);

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

    readonly record struct Match(IOutput? Output, int Candidate, int Variant, int Kind)
    {
        public static Match Empty => new(null, int.MaxValue, int.MaxValue, int.MaxValue);
    }
}
