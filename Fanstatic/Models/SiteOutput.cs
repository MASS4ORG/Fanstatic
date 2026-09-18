global using SiteOutputVariant = (string outputFormat, string? language);
using System.Collections.Concurrent;
using Serilog;
using Fanstatic.Helpers;
using Fanstatic.Parsers;
using Fanstatic.TemplateEngine;


namespace Fanstatic.Models;

public class SiteOutput(ISite siteImplementation, SiteOutputVariant variant) : ISiteOutput
{
    public LanguageSettings Language => siteImplementation.GetLanguage(variant.language);

    public IReadOnlyList<LanguageSettings> Languages => siteImplementation.LanguageList;

    public string OutputFormat => variant.outputFormat;

    public Dictionary<string, object> Params
    {
        get => siteImplementation.Params;
        set => siteImplementation.Params = value;
    }

    public string Title => siteImplementation.Title;

    public string? Description => siteImplementation.Description;

    public string? Copyright => siteImplementation.Copyright;

    public Uri BaseUrl => (siteImplementation as ISiteOutput).BaseUrl;

    public bool UglyUrLs => siteImplementation.UglyUrLs;

    public int Paginate => siteImplementation.Paginate;

    public string PaginatePath => siteImplementation.PaginatePath;

    public Dictionary<Kind, List<string>> KindOutputFormats => siteImplementation.KindOutputFormats;

    public FanstaticInfo Fanstatic => siteImplementation.Fanstatic;

    public Theme? Theme => siteImplementation.Theme;

    public string SourceContentPath => siteImplementation.SourceContentPath;

    public string SourceStaticPath => siteImplementation.SourceStaticPath;

    public string SourceThemePath => siteImplementation.SourceThemePath;

    public ConcurrentDictionary<Uri, IOutput> OutputReferences => siteImplementation.OutputReferences;

    /// <inheritdoc/>
    public IEnumerable<IPage> Pages =>
        siteImplementation.Pages
            .Where(output => output.OutputFormat == OutputFormat && IsSameLanguage(output));

    public IEnumerable<IPage> RegularPages =>
        siteImplementation.RegularPages
            .Where(output => output.OutputFormat == OutputFormat && IsSameLanguage(output));

    // AllRegularPages intentionally spans every language: it is the cross-cutting accessor
    // used by outputs such as sitemaps that should list the whole site.
    public IEnumerable<IPage> AllRegularPages =>
        siteImplementation.RegularPages.Where(p => p.OutputFormat == "html");

    bool IsSameLanguage(IPage page) =>
        string.Equals(page.ContentSource.Language, Language.Code, StringComparison.OrdinalIgnoreCase);

    public IPage? Home => siteImplementation.Home;

    public SiteCacheManager CacheManager => siteImplementation.CacheManager;

    public IFrontMatterParser Parser => siteImplementation.Parser;

    public ITemplateEngine TemplateEngine => siteImplementation.TemplateEngine;

    public ILogger Logger => siteImplementation.Logger;

    public IEnumerable<string> SourceFolders => siteImplementation.SourceFolders;

    public void ProcessPages() => siteImplementation.ProcessPages();
}
