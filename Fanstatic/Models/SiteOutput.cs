global using SiteOutputVariant = (string outputFormat, string? language);
using System.Collections.Concurrent;
using Fanstatic.Helpers;
using Fanstatic.Parsers;
using Fanstatic.TemplateEngine;
using Serilog;


namespace Fanstatic.Models;

public class SiteOutput : ISiteOutput
{
    readonly ISite _siteImplementation;

    readonly SiteOutputVariant _variant;

    readonly Lazy<IReadOnlyDictionary<string, TaxonomyTerms>> _taxonomiesCached;

    readonly Lazy<IReadOnlyList<IPage>> _regularPagesByDateCached;

    readonly Lazy<IReadOnlyList<IPage>> _regularPagesByLastModCached;

    readonly Lazy<IReadOnlyList<IPage>> _regularPagesByWeightCached;

    readonly Lazy<IReadOnlyList<IPage>> _regularPagesByTitleCached;

    public SiteOutput(ISite siteImplementation, SiteOutputVariant variant)
    {
        _siteImplementation = siteImplementation;
        _variant = variant;
        _taxonomiesCached = new(CreateTaxonomies);
        _regularPagesByDateCached = new(() => RegularPages.OrderBy(page => page.Date).ToList());
        _regularPagesByLastModCached = new(() => RegularPages.OrderBy(page => page.LastMod).ToList());
        _regularPagesByWeightCached = new(() => RegularPages.OrderBy(page => page.Weight).ToList());
        _regularPagesByTitleCached = new(() => RegularPages.OrderBy(page => page.Title).ToList());
    }

    public LanguageSettings Language => _siteImplementation.GetLanguage(_variant.language);

    public IReadOnlyList<LanguageSettings> Languages => _siteImplementation.LanguageList;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, TaxonomyTerms> Taxonomies
        => _taxonomiesCached.Value;

    IReadOnlyDictionary<string, TaxonomyTerms> CreateTaxonomies()
    {
        var taxonomies = new Dictionary<string, TaxonomyTerms>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var plural in _siteImplementation.TaxonomyDefinitions.Values.Distinct(
                     StringComparer.OrdinalIgnoreCase))
        {
            var terms = _siteImplementation.Pages
                .Where(page => page.Kind == Kind.term
                               && page.Section?.Equals(plural, StringComparison.OrdinalIgnoreCase) == true
                               && page.OutputFormat == OutputFormat
                               && IsSameLanguage(page))
                .DistinctBy(page => page.ContentSource)
                .Select(page => new TaxonomyTerm(page));
            taxonomies[plural] = new TaxonomyTerms(terms);
        }

        return taxonomies;
    }

    public string OutputFormat => _variant.outputFormat;

    public Dictionary<string, object> Params
    {
        get => _siteImplementation.Params;
        init => _siteImplementation.Params = value;
    }

    public string Title => _siteImplementation.Title;

    public string? Description => _siteImplementation.Description;

    public string? Copyright => _siteImplementation.Copyright;

    public Uri BaseUrl => (_siteImplementation as ISiteOutput).BaseUrl;

    public bool UglyUrLs => _siteImplementation.UglyUrLs;

    public int Paginate => _siteImplementation.Paginate;

    public string PaginatePath => _siteImplementation.PaginatePath;

    public Dictionary<Kind, List<string>> KindOutputFormats => _siteImplementation.KindOutputFormats;

    public FanstaticInfo Fanstatic => _siteImplementation.Fanstatic;

    public Theme? Theme => _siteImplementation.Theme;

    public string SourceContentPath => _siteImplementation.SourceContentPath;

    public string SourceStaticPath => _siteImplementation.SourceStaticPath;

    public string SourceThemePath => _siteImplementation.SourceThemePath;

    public ConcurrentDictionary<Uri, IOutput> OutputReferences => _siteImplementation.OutputReferences;

    /// <inheritdoc/>
    public IEnumerable<IPage> Pages =>
        _siteImplementation.Pages
            .Where(output => output.OutputFormat == OutputFormat && IsSameLanguage(output));

    public IEnumerable<IPage> RegularPages =>
        _siteImplementation.RegularPages
            .Where(output => output.OutputFormat == OutputFormat && IsSameLanguage(output));

    /// <summary>
    /// Regular pages ordered by date and cached for this output _variant.
    /// </summary>
    public IReadOnlyList<IPage> RegularPagesByDate => _regularPagesByDateCached.Value;

    /// <summary>
    /// Regular pages ordered by last modification date and cached for this output _variant.
    /// </summary>
    public IReadOnlyList<IPage> RegularPagesByLastMod => _regularPagesByLastModCached.Value;

    /// <summary>
    /// Regular pages ordered by weight and cached for this output _variant.
    /// </summary>
    public IReadOnlyList<IPage> RegularPagesByWeight => _regularPagesByWeightCached.Value;

    /// <summary>
    /// Regular pages ordered by title and cached for this output _variant.
    /// </summary>
    public IReadOnlyList<IPage> RegularPagesByTitle => _regularPagesByTitleCached.Value;

    // AllRegularPages intentionally spans every language: it is the cross-cutting accessor
    // used by outputs such as sitemaps that should list the whole site.
    public IEnumerable<IPage> AllRegularPages =>
        _siteImplementation.RegularPages.Where(p => p.OutputFormat == "html");

    bool IsSameLanguage(IPage page) =>
        string.Equals(page.ContentSource.Language, Language.Code, StringComparison.OrdinalIgnoreCase);

    public IPage? Home => _siteImplementation.Home;

    public SiteCacheManager CacheManager => _siteImplementation.CacheManager;

    public IFrontMatterParser Parser => _siteImplementation.Parser;

    public ITemplateEngine TemplateEngine => _siteImplementation.TemplateEngine;

    public ILogger Logger => _siteImplementation.Logger;

    public IEnumerable<string> SourceFolders => _siteImplementation.SourceFolders;

    public void ProcessPages() => _siteImplementation.ProcessPages();
}
