using Serilog;
using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Parsers;
using Fanstatic.TemplateEngine;

namespace Fanstatic.Models;

/// <summary>
/// The main configuration of the program, primarily extracted from the app.yaml file.
/// </summary>
public interface ISite : ISiteSettings, ISiteOutput
{
    /// <summary>
    /// Command line options
    /// </summary>
    IGenerateOptions Options { get; set; }

    /// <summary>
    /// The path of the content, based on the source path.
    /// </summary>
    string SourceContentPath { get; }

    /// <summary>
    /// The path of the static content (that will be copied as is), based on the source path.
    /// </summary>
    string SourceStaticPath { get; }

    /// <summary>
    /// The path theme.
    /// </summary>
    string SourceThemePath { get; }

    /// <summary>
    /// Manage all caching lists for the site
    /// </summary>
    SiteCacheManager CacheManager { get; }

    /// <summary>
    /// Front Matter parser
    /// </summary>
    IFrontMatterParser Parser { get; }

    /// <summary>
    /// The template engine.
    /// </summary>
    ITemplateEngine TemplateEngine { get; }

    /// <summary>
    /// The logger instance.
    /// </summary>
    ILogger Logger { get; }

    /// <summary>
    /// List of all basic source folders
    /// </summary>
    IEnumerable<string> SourceFolders { get; }

    /// <summary>
    /// All resolved languages, sorted by weight then code.
    /// </summary>
    IReadOnlyList<LanguageSettings> LanguageList { get; }

    /// <summary>
    /// The resolved default language.
    /// </summary>
    LanguageSettings DefaultLanguageObj { get; }

    /// <summary>
    /// True when the site declares more than one language.
    /// </summary>
    bool IsMultilingual { get; }

    /// <summary>
    /// Returns the resolved language for the given code, falling back to the default.
    /// </summary>
    LanguageSettings GetLanguage(string? code);

    /// <summary>
    /// Translation strings loaded from the <c>i18n/</c> directory, keyed by language
    /// code then by translation key. Used by the <c>i18n</c> Liquid filter.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, I18NEntry>> I18N { get; }

    /// <summary>
    /// True when the code matches a language explicitly configured under <c>languages</c>.
    /// </summary>
    bool IsConfiguredLanguage(string code);

    /// <summary>
    /// Resets the template cache to force a reload of all templates.
    /// </summary>
    void ResetCache();

    /// <summary>
    /// Search recursively for all markdown files in the content folder, then
    /// parse their content for front matter and markdown.
    /// </summary>
    /// <param name="fs"></param>
    /// <param name="directory">Folder to scan</param>
    /// <param name="level">Folder recursive level</param>
    /// <param name="parent">Page of the upper directory</param>
    /// <param name="cascade"></param>
    /// <returns></returns>
    void ScanAndParseSourceFiles(
        IFileSystem fs,
        string? directory = null,
        int level = 0,
        ContentSource? parent = null,
        FrontMatter? cascade = null);

    /// <summary>
    /// Groups content sources that are translations of each other. Must be called
    /// after all source files have been scanned and before pages are processed.
    /// </summary>
    void BuildTranslationGroups();

    /// <summary>
    /// Expand the front matter to full-blown pages.
    /// </summary>
    void ProcessPages();

    /// <summary>
    /// Register paginated URL entries in OutputReferences after all pages are created.
    /// Must be called after ProcessPages so ContentSourceToPages is fully populated.
    /// </summary>
    void RegisterPaginatedUrls();

    /// <summary>
    /// Gets the Permalink path for the file.
    /// </summary>
    /// <param name="page"></param>
    /// <param name="urlForce">The URL to consider. If null use the predefined URL</param>
    /// <returns>The output path.</returns>
    Uri CreatePermalink(Page page, string? urlForce = null);

    /// <summary>
    /// Extra calculation and automatic data for each page.
    /// </summary>
    /// <param name="page">The given page to be processed</param>
    /// <param name="overwrite"></param>
    void PostProcessPage(in IPage page, bool overwrite = false);

    /// <summary>
    /// Check if the page have the conditions to be published: valid date and not draft,
    /// unless a command line option to force it.
    /// </summary>
    /// <param name="frontMatter">Page or front matter</param>
    /// <param name="options">options</param>
    /// <returns></returns>
    bool IsPageValid(in IContentSource frontMatter, IGenerateOptions? options);

    /// <summary>
    /// Check if the page have a publishing date from the past.
    /// </summary>
    /// <param name="contentSource">Page or content Source</param>
    /// <param name="options">options</param>
    /// <returns></returns>
    bool IsDateValid(in IContentSource contentSource, IGenerateOptions? options);

    /// <summary>
    /// Check if the page is expired
    /// </summary>
    bool IsDateExpired(in IContentSource contentSource);

    /// <summary>
    /// Check if the page is publishable
    /// </summary>
    bool IsDatePublishable(in IContentSource contentSource);

    /// <summary>
    /// Create a Page from front matter
    /// </summary>
    /// <param name="contentSource"></param>
    List<Page> PageCreate(ContentSource contentSource);

    /// <summary>
    /// Include the Front Matter into the site
    /// </summary>
    /// <param name="contentSource"></param>
    ContentSource? ContentSourceAdd(ContentSource? contentSource);

    string ParseAndRenderTemplate(Page page, bool b);
}
