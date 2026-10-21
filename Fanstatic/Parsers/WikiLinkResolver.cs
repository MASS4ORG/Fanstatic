using Fanstatic.Models;

namespace Fanstatic.Parsers;

/// <summary>
/// Resolves the target of an Obsidian-style <c>[[wikilink]]</c> into a link.
/// Accepted forms: <c>[[tags/name]]</c> (a taxonomy term), <c>[[posts/file.md]]</c> or
/// <c>[[/posts/file.md]]</c> (a content path, like <c>ref</c>/<c>relref</c>), and a bare
/// <c>[[name]]</c>, which tries a taxonomy term first and then a page.
/// </summary>
public static class WikiLinkResolver
{
    /// <summary>
    /// Resolves <paramref name="target"/> to a URL relative to the site root, or <c>./</c> when it does not
    /// match a term or a page. Unresolved targets are logged as errors on <paramref name="site"/>.
    /// </summary>
    /// <param name="site">The site holding the pages and taxonomies.</param>
    /// <param name="page">The calling page, whose language and output format are the defaults.</param>
    /// <param name="target">The wikilink target, without the surrounding brackets or a display label.</param>
    public static string Resolve(ISite site, IPage page, string? target)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(page);

        var trimmed = target?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return Report(site, page, target);
        }

        if (TryResolveTerm(site, page, trimmed, out var termUrl))
        {
            return termUrl;
        }

        var pageMatch = RefShortcodeParser.ResolvePage(site, page, trimmed);
        if (pageMatch is not null)
        {
            return pageMatch.RelPermalink.ToString();
        }

        return Report(site, page, trimmed);
    }

    /// <summary>
    /// Resolves a term target, either as a bare name or as <c>&lt;plural&gt;/&lt;name&gt;</c>.
    /// </summary>
    static bool TryResolveTerm(ISite site, IPage page, string target, out string url)
    {
        url = string.Empty;
        var (plural, name) = SplitPlural(target);
        if (name.Length == 0)
        {
            return false;
        }

        foreach (var (taxonomyName, taxonomyPlural) in site.TaxonomyDefinitions)
        {
            if (!MatchesPlural(plural, taxonomyName, taxonomyPlural))
            {
                continue;
            }

            if (!page.Site.Taxonomies.TryGetValue(taxonomyPlural, out var terms))
            {
                continue;
            }

            var match = terms.FirstOrDefault(term =>
                term.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                || term.Title.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                url = match.Page.RelPermalink.ToString();
                return true;
            }
        }

        return false;
    }

    static bool MatchesPlural(string plural, string taxonomyName, string taxonomyPlural) =>
        plural.Length == 0
        || plural.Equals(taxonomyPlural, StringComparison.OrdinalIgnoreCase)
        || plural.Equals(taxonomyName, StringComparison.OrdinalIgnoreCase);

    static (string Plural, string Name) SplitPlural(string target)
    {
        var slash = target.IndexOf('/');
        return slash <= 0 ? (string.Empty, target.Trim('/')) : (target[..slash], target[(slash + 1)..].Trim('/'));
    }

    static string Report(ISite site, IPage page, string? target)
    {
        var sourcePath = page.SourceFullPath(site.SourceContentPath);
        var message = $"wikilink: unable to resolve '{target}' referenced in {sourcePath}";
        if (site.MissingWikiLinkAsError)
        {
            throw new InvalidOperationException(message);
        }

        site.Logger.Error("wikilink: unable to resolve {Target} referenced in {File}", target ?? string.Empty,
            sourcePath);
        return "./";
    }
}
