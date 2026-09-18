using System.Text.RegularExpressions;
using Fanstatic.Helpers;
using Fanstatic.Models;

namespace Fanstatic.Parsers;

/// <summary>
/// Resolves Hugo-style <c>{{&lt; ref &gt;}}</c> / <c>{{&lt; relref &gt;}}</c> shortcodes found in
/// raw markdown content into permalinks, before the content is handed to Markdig.
/// <c>ref</c> resolves to the absolute permalink, <c>relref</c> to the relative one. The target
/// page is looked up using the calling page's output format and language, unless overridden with
/// the <c>outputFormat</c> and <c>lang</c> named arguments.
/// </summary>
/// Usage: <c>{{&lt; ref "posts/my-post.md" &gt;}}</c>,
/// <c>{{&lt; relref path="/posts/my-post.md" lang="pt-br" outputFormat="rss" &gt;}}</c>.
public static partial class RefShortcodeParser
{
    [GeneratedRegex(@"\{\{<\s*(ref|relref)\s+(?<args>[^>]*?)\s*>\}\}", RegexOptions.IgnoreCase)]
    private static partial Regex ShortcodeRegex();

    [GeneratedRegex("""(?<key>[a-zA-Z]+)\s*=\s*(?:"(?<val>[^"]*)"|'(?<val>[^']*)')""")]
    private static partial Regex NamedArgRegex();

    [GeneratedRegex("""^\s*(?:"(?<val>[^"]*)"|'(?<val>[^']*)')""")]
    private static partial Regex LeadingQuotedArgRegex();

    /// <summary>
    /// Replaces every <c>ref</c>/<c>relref</c> shortcode occurrence in <paramref name="content"/>
    /// with the resolved permalink of the target page.
    /// </summary>
    public static string Process(string content, ISite site, IPage page)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(page);

        return string.IsNullOrEmpty(content) || !content.Contains("{{<", StringComparison.Ordinal)
            ? content
            : ShortcodeRegex().Replace(content, match => ResolveMatch(match, site, page));
    }

    static string ResolveMatch(Match match, ISite site, IPage page)
    {
        var isAbsolute = match.Groups[1].Value.Equals("ref", StringComparison.OrdinalIgnoreCase);
        var (path, lang, outputFormat) = ParseArgs(match.Groups["args"].Value);

        if (string.IsNullOrWhiteSpace(path))
        {
            site.Logger.Error("ref/relref shortcode with no path found in {file}", page.SourceRelativePath);
            return match.Value;
        }

        var (targetPath, anchor) = SplitAnchor(path);
        var target = FindPage(site, page, targetPath, lang ?? page.ContentSource.Language,
            outputFormat ?? page.OutputFormat);

        if (target is null)
        {
            site.Logger.Error("ref/relref: unable to resolve {path} referenced in {file}", path,
                page.SourceRelativePath);
            return "#";
        }

        var url = (isAbsolute ? target.Permalink : target.RelPermalink).ToString();
        return string.IsNullOrEmpty(anchor) ? url : $"{url}#{anchor}";
    }

    static (string path, string? lang, string? outputFormat) ParseArgs(string args)
    {
        string? path = null;
        var remainder = args;

        var leading = LeadingQuotedArgRegex().Match(args);
        if (leading.Success)
        {
            path = leading.Groups["val"].Value;
            remainder = args[leading.Length..];
        }

        string? lang = null;
        string? outputFormat = null;
        foreach (var namedMatch in NamedArgRegex().Matches(remainder).Cast<Match>())
        {
            var value = namedMatch.Groups["val"].Value;
            switch (namedMatch.Groups["key"].Value)
            {
                case "path" when path is null:
                    path = value;
                    break;
                case "lang":
                    lang = value;
                    break;
                case "outputFormat" or "outputformat":
                    outputFormat = value;
                    break;
            }
        }

        return (path ?? string.Empty, lang, outputFormat);
    }

    static (string path, string anchor) SplitAnchor(string path)
    {
        var hashIndex = path.IndexOf('#');
        return hashIndex < 0 ? (path, string.Empty) : (path[..hashIndex], path[(hashIndex + 1)..]);
    }

    /// <summary>
    /// Looks up a page by content path, first among the exact language/outputFormat requested,
    /// falling back to any other matching variant (e.g. when a translation is missing).
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

            fallback ??= candidate;
        }

        return fallback;
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
