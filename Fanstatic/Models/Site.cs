using System.Collections.Concurrent;
using Serilog;
using Fanstatic.Commands;
using Fanstatic.Helpers;
using Fanstatic.Parsers;
using Fanstatic.TemplateEngine;
using YamlDotNet.Serialization;

namespace Fanstatic.Models;

/// <summary>
/// The main configuration of the program, primarily extracted from the app.yaml file.
/// </summary>
public class Site : ISite
{
    Dictionary<SiteOutputVariant, SiteOutput> _siteVariants = new();

    #region IParams

    /// <inheritdoc/>
    public Dictionary<string, object> Params
    {
        get => _settings.Params;
        set => _settings.Params = value;
    }

    #endregion IParams

    #region SiteSettings

    /// <inheritdoc/>
    public string Title => _settings.Title;

    /// <inheritdoc/>
    public string? Description => _settings.Description;

    /// <inheritdoc/>
    public string? Copyright => _settings.Copyright;

    /// <inheritdoc/>
    public Uri BaseUrl
    {
        get => _settings.BaseUrl;
        set => _settings.BaseUrl = value;
    }

    /// <inheritdoc/>
    public bool UglyUrLs => _settings.UglyUrLs;

    /// <inheritdoc/>
    public int Paginate => _settings.Paginate;

    /// <inheritdoc/>
    public string PaginatePath => _settings.PaginatePath;

    /// <inheritdoc/>
    public Dictionary<Kind, List<string>> KindOutputFormats =>
        _settings.KindOutputFormats;

    /// <inheritdoc/>
    public Dictionary<string, LanguageSettings> Languages => _settings.Languages;

    /// <inheritdoc/>
    public string DefaultLanguage => _settings.DefaultLanguage;

    /// <inheritdoc/>
    public bool DefaultContentLanguageInSubdir => _settings.DefaultContentLanguageInSubdir;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, I18NEntry>> I18N { get; private set; }
        = new Dictionary<string, IReadOnlyDictionary<string, I18NEntry>>();

    #endregion SiteSettings

    #region Languages

    /// <summary>
    /// The resolved languages, keyed by code, with fallbacks to the root settings applied.
    /// </summary>
    readonly Dictionary<string, LanguageSettings> _languagesResolved = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    LanguageSettings ISiteOutput.Language => DefaultLanguageObj;

    /// <inheritdoc/>
    IReadOnlyList<LanguageSettings> ISiteOutput.Languages => LanguageList;

    /// <summary>
    /// All resolved languages, sorted by weight then code.
    /// </summary>
    public IReadOnlyList<LanguageSettings> LanguageList { get; private set; } = [];

    /// <summary>
    /// The resolved default language.
    /// </summary>
    public LanguageSettings DefaultLanguageObj { get; private set; } = new();

    /// <summary>
    /// True when the site declares more than one language.
    /// </summary>
    public bool IsMultilingual => _settings.Languages.Count > 1;

    /// <summary>
    /// Returns the resolved language for the given code, falling back to the default language.
    /// </summary>
    public LanguageSettings GetLanguage(string? code) =>
        code is not null && _languagesResolved.TryGetValue(code, out var language)
            ? language
            : DefaultLanguageObj;

    /// <summary>
    /// True when the given code matches a language explicitly configured under the
    /// <c>languages</c> key (used to recognize filename language suffixes).
    /// </summary>
    public bool IsConfiguredLanguage(string code) => _settings.Languages.ContainsKey(code);

    void BuildLanguages()
    {
        _languagesResolved.Clear();

        if (_settings.Languages.Count == 0)
        {
            var only = new LanguageSettings
            {
                Code = _settings.DefaultLanguage,
                Title = _settings.Title,
                Description = _settings.Description,
                BaseUrl = _settings.BaseUrl,
                LanguageName = _settings.DefaultLanguage,
                IsDefault = true
            };
            _languagesResolved[only.Code] = only;
        }
        else
        {
            foreach (var (code, language) in _settings.Languages)
            {
                language.Code = code;
                language.Title ??= _settings.Title;
                language.Description ??= _settings.Description;
                language.BaseUrl ??= _settings.BaseUrl;
                language.LanguageName ??= code;
                language.IsDefault =
                    string.Equals(code, _settings.DefaultLanguage, StringComparison.OrdinalIgnoreCase);
                _languagesResolved[code] = language;
            }

            if (!_languagesResolved.ContainsKey(_settings.DefaultLanguage))
            {
                var synthesizedDefault = new LanguageSettings
                {
                    Code = _settings.DefaultLanguage,
                    Title = _settings.Title,
                    Description = _settings.Description,
                    BaseUrl = _settings.BaseUrl,
                    LanguageName = _settings.DefaultLanguage,
                    IsDefault = true
                };
                _languagesResolved[synthesizedDefault.Code] = synthesizedDefault;
            }
        }

        foreach (var language in _languagesResolved.Values)
        {
            var noPrefix = language.IsDefault && !_settings.DefaultContentLanguageInSubdir;
            language.RelPermalink = new Uri(noPrefix ? "/" : $"/{language.Code}/", UriKind.Relative);
        }

        LanguageList = _languagesResolved.Values
            .OrderBy(language => language.Weight)
            .ThenBy(language => language.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        DefaultLanguageObj = GetLanguageOrFirst(_settings.DefaultLanguage);
    }

    LanguageSettings GetLanguageOrFirst(string code) =>
        _languagesResolved.TryGetValue(code, out var language) ? language : LanguageList[0];

    /// <summary>
    /// Loads translation strings from <c>i18n/*.yaml</c> files.
    /// Each file is named <c>{languageCode}.yaml</c> and contains a flat or
    /// Hugo-compatible mapping of translation keys to their localized values.
    /// </summary>
    public void LoadTranslations()
    {
        var i18NDir = Path.Combine(Options.Source, "i18n");
        if (!Directory.Exists(i18NDir))
        {
            return;
        }

        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        var result = new Dictionary<string, IReadOnlyDictionary<string, I18NEntry>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(i18NDir, "*.yaml"))
        {
            var langCode = Path.GetFileNameWithoutExtension(file);
            var yamlContent = File.ReadAllText(file);
            var raw = deserializer.Deserialize<Dictionary<string, object?>>(yamlContent);
            if (raw is null)
            {
                continue;
            }

            var entries = new Dictionary<string, I18NEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in raw)
            {
                I18NEntry entry;
                if (value is string s)
                {
                    entry = new I18NEntry { Other = s };
                }
                else if (value is Dictionary<object, object?> dict)
                {
                    entry = new I18NEntry
                    {
                        One = dict.TryGetValue("one", out var one) ? one?.ToString() : null,
                        Other = dict.TryGetValue("other", out var other) ? other?.ToString() : null
                    };
                }
                else
                {
                    continue;
                }

                entries[key] = entry;
            }

            result[langCode] = entries;
        }

        I18N = result;
    }

    #endregion Languages

    #region ISite

    /// <inheritdoc/>
    public FanstaticInfo Fanstatic { get; } = new FanstaticInfo();

    /// <inheritdoc/>
    public IGenerateOptions Options { get; set; }

    /// <inheritdoc/>
    public Theme? Theme { get; }

    /// <inheritdoc/>
    public string SourceContentPath => Path.Combine(Options.Source, "content");

    /// <inheritdoc/>
    public string SourceStaticPath => Path.Combine(Options.Source, "static");

    /// <inheritdoc/>
    public string SourceThemePath => Path.Combine(Options.Source,
        _settings.ThemeDir, _settings.Theme ?? string.Empty);

    /// <inheritdoc/>
    public ConcurrentDictionary<Uri, IOutput> OutputReferences { get; } = [];

    /// <inheritdoc/>
    public IEnumerable<IPage> Pages
    {
        get
        {
            _pagesCache ??= OutputReferences.Values
                .Where(output => output is IPage)
                .Select(output => (output as IPage)!)
                .OrderBy(page => -page.Weight);
            return _pagesCache!;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<IPage> AllRegularPages =>
        RegularPages.Where(p => p.OutputFormat == "html");

    /// <inheritdoc/>
    public IEnumerable<IPage> RegularPages
    {
        get
        {
            _regularPagesCache ??= OutputReferences
                .Where(pair =>
                    pair.Value is IPage
                    {
                        IsPage: true
                    } page &&
                    pair.Key == page.RelPermalink)
                .Select(pair => (pair.Value as IPage)!)
                .OrderBy(page => -page.Weight);
            return _regularPagesCache;
        }
    }

    /// <inheritdoc/>
    public IPage? Home { get; private set; }

    /// <inheritdoc/>
    public SiteCacheManager CacheManager { get; } = new();

    /// <inheritdoc/>
    public IFrontMatterParser Parser { get; }

    /// <inheritdoc/>
    public ITemplateEngine TemplateEngine { get; }

    /// <inheritdoc/>
    public ILogger Logger { get; }

    /// <inheritdoc/>
    public IEnumerable<string> SourceFolders =>
    [
        SourceContentPath,
        SourceStaticPath,
        SourceThemePath
    ];

    /// <inheritdoc/>
    public void ResetCache()
    {
        CacheManager.ResetCache();
        OutputReferences.Clear();
    }

    #endregion

    /// <summary>
    /// Number of files parsed, used in the report.
    /// </summary>
    public int FilesParsedToReport => _filesParsedToReport;

    int _filesParsedToReport;

    const string IndexLeafFileConst = "index.md";

    const string IndexBranchFileConst = "_index.md";

    /// <summary>
    /// The synchronization lock object during PostProcess.
    /// </summary>
    readonly Lock _syncLockPostProcess = new();

    IEnumerable<IPage>? _pagesCache;

    IEnumerable<IPage>? _regularPagesCache;

    readonly SiteSettings _settings;

    readonly ISystemClock _clock;

    readonly ConcurrentDictionary<string, ContentSource> _contentSources = [];

    /// <summary>
    /// Constructor
    /// </summary>
    public Site(
        in IGenerateOptions options,
        in SiteSettings settings,
        in IFrontMatterParser parser,
        in ILogger logger,
        ISystemClock? clock)
    {
        Options = options;
        _settings = settings;
        Logger = logger;
        Parser = parser;
        TemplateEngine = new FluidTemplateEngine();

        _clock = clock ?? new SystemClock();

        BuildLanguages();
        LoadTranslations();

        Theme = Theme.CreateFromSite(this);
    }

    #region ISite methods

    /// <inheritdoc/>
    public void ScanAndParseSourceFiles(IFileSystem fs, string? directory,
        int level = 0, ContentSource? parent = null, FrontMatter? cascade = null)
    {
        ArgumentNullException.ThrowIfNull(fs);

        directory ??= SourceContentPath;

        cascade ??= new FrontMatter();

        var markdownFiles = fs.DirectoryGetFiles(directory, "*.md").ToList();
        ParseIndexFrontMatter(directory, level, ref parent, ref cascade,
            ref markdownFiles);

        // Other source files that are not index
        // _ = Parallel.ForEach(markdownFiles,
        markdownFiles.ForEach(filePath =>
        {
            var (frontMatter, rawContent) = ParseFile(filePath, cascade);
            if (frontMatter is null)
            {
                return;
            }

            var contentSource = new ContentSource(Path.GetRelativePath(SourceContentPath, filePath), frontMatter,
                    rawContent)
                .ScanForResources(this);
            contentSource.ContentSourceParent = parent;

            ContentSourceAdd(contentSource);
        });

        var subdirectories = fs.DirectoryGetDirectories(directory);
        foreach (var subdirectory in subdirectories)
        {
            ScanAndParseSourceFiles(fs, subdirectory, level + 1, parent,
                cascade);
        }
    }

    /// <inheritdoc/>
    public void PostProcessPage(in IPage page, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(page);

        var relPermalink = CreatePermalink((Page)page);
        page.RelPermalink = relPermalink;
        lock (_syncLockPostProcess)
        {
            ProcessPageOutput(page, overwrite);

            if (DefaultContentLanguageInSubdir
                && page.IsDefaultLanguage
                && IsMultilingual)
            {
                var prefixedUrl = $"/{page.ContentSource.Language}{relPermalink}";
                var prefixedUri = new Uri(prefixedUrl, UriKind.Relative);
                if (!OutputReferences.ContainsKey(prefixedUri))
                {
                    _ = OutputReferences.TryAdd(prefixedUri, page);
                }
            }
        }

        ProcessSectionReference(page);
    }

    /// <inheritdoc/>
    public Uri CreatePermalink(Page page, string? urlTemplate = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        var relPermalink = "/";

        if ((page as IFile).SourceFullPathDirectory(SourceContentPath) !=
            "/")
        {
            urlTemplate ??= page.UrlTemplate;

            try
            {
                relPermalink = TemplateEngine.RenderInline(urlTemplate!, this, page);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error converting URL: {UrlForce}", urlTemplate);
            }

            if (!relPermalink.StartsWith('/'))
            {
                relPermalink = $"/{relPermalink}";
            }
        }

        var useUgly = (!page.OutputFormatObj.NoUgly && (page.OutputFormatObj.Ugly || UglyUrLs));

        var relPermalinkDir = UrlExtension.SanitizeUrlPath(relPermalink);
        relPermalinkDir = (relPermalinkDir.EndsWith('/')
            ? relPermalinkDir
            : relPermalinkDir + "/");
        var relPermalinkFilename = UrlExtension.SanitizeUrlPath(useUgly
            ? $"{page.SourceFileNameWithoutExtension}.{page.OutputFormatObj.Extension}"
            : $"{page.OutputFormatObj.BaseName}.{page.OutputFormatObj.Extension}");

        var urlFinal = relPermalinkDir + relPermalinkFilename;

        urlFinal = ApplyLanguagePrefix(urlFinal, page.ContentSource.Language);

        return new Uri(urlFinal, UriKind.Relative);
    }

    /// <summary>
    /// Prepends the language code to the URL for non-default languages. The prefix is
    /// only added once: URLs whose parent chain already carries it are left untouched.
    /// </summary>
    string ApplyLanguagePrefix(string url, string language)
    {
        var isDefault = string.Equals(language, DefaultLanguage, StringComparison.OrdinalIgnoreCase);
        if (!IsMultilingual || isDefault)
        {
            return url;
        }

        var prefix = "/" + language;
        if (url.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || url.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return prefix + url;
    }

    /// <inheritdoc />
    public bool IsPageValid(in IContentSource contentSource, IGenerateOptions? options)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        return IsDateValid(contentSource, options) &&
               (contentSource.Draft is null || contentSource.Draft == false || (options?.Draft ?? false));
    }

    /// <inheritdoc />
    public bool IsDateValid(in IContentSource contentSource, IGenerateOptions? options)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        return (!IsDateExpired(contentSource) || (options?.Expired ?? false))
               && (IsDatePublishable(contentSource) ||
                   (options?.Future ?? false));
    }

    /// <inheritdoc />
    public bool IsDateExpired(in IContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        return contentSource.ExpiryDate is not null && contentSource.ExpiryDate <= _clock.Now;
    }

    /// <inheritdoc />
    public bool IsDatePublishable(in IContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        return contentSource.GetPublishDate is null || contentSource.GetPublishDate <= _clock.Now;
    }

    #endregion ISite methods

    /// <inheritdoc/>
    public List<Page> PageCreate(ContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        List<Page> pages = [];

        // Create the parent if it does not exist
        if (contentSource.ContentSourceParent is
            {
                ContentSourceToPages.Count: 0
            })
        {
            var parents = PageCreate(contentSource.ContentSourceParent);
            pages.AddRange(parents);
            contentSource.ContentSourceParent.ContentSourceToPages.AddRange(parents);
        }

        var outputFormats = GetUniqueOutputFormats(contentSource.Kind, KindOutputFormats).ToList();

        if (IsPageValid(contentSource, Options))
        {
            foreach (var outputFormat in outputFormats)
            {
                SiteOutputVariant siteVariant = (outputFormat, contentSource.Language);
                _siteVariants.TryGetValue(siteVariant, out var siteOutput);
                if (siteOutput is null)
                {
                    siteOutput = new SiteOutput(this, siteVariant);
                    _siteVariants.Add(siteVariant, siteOutput);
                }

                Page page = new(contentSource, this, siteOutput, siteVariant, outputFormats);
                PostProcessPage(page, true);
                contentSource.ContentSourceToPages.Add(page);
                pages.Add(page);

                if (Home is null && page.SourceRelativePath is IndexBranchFileConst or IndexLeafFileConst)
                {
                    Home = page;
                }
            }
        }

        // Use interlocked to safely increment the counter in a multithreaded environment
        _ = Interlocked.Increment(ref _filesParsedToReport);

        return pages;
    }

    /// <summary>
    /// Create pages from content source
    /// </summary>
    public void ProcessPages() =>
        _contentSources
            .Where(cs => cs.Value.ContentSourceToPages.Count == 0)
            .OrderBy(cs => cs.Value.BundleType == BundleType.none)
            .ThenBy(cs => cs.Value.SourceRelativePathDirectory)
            .Select(cs => cs.Value)
            .ToList()
            .ForEach(cs => PageCreate(cs));

    /// <inheritdoc/>
    public void RegisterPaginatedUrls()
    {
        // Snapshot to avoid modifying OutputReferences while iterating it.
        var pages = OutputReferences.Values.OfType<Page>().ToList();
        foreach (var page in pages)
        {
            if (page.OutputFormatObj.NoUgly) continue;
            var pager = page.Paginator;
            if (pager is null || pager.Count <= 1) continue;

            var filename = $"{page.OutputFormatObj.BaseName}.{page.OutputFormatObj.Extension}";
            SiteOutputVariant siteVariant = (page.OutputFormat, page.ContentSource.Language);
            _siteVariants.TryGetValue(siteVariant, out var siteOutput);
            if (siteOutput is null) continue;

            for (var i = 2; i <= pager.Count; i++)
            {
                var url = new Uri(
                    $"{pager.BaseUrl.TrimEnd('/')}/{pager.PaginatePath}/{i}/{filename}",
                    UriKind.Relative);
                var paginatedPage = new Page(page.ContentSource, this, siteOutput, siteVariant, page.OutputFormats)
                {
                    PageIndex = i,
                    RelPermalink = url
                };
                OutputReferences.TryAdd(url, paginatedPage);
            }
        }
    }

    /// <summary>
    /// Called by the <c>paginate</c> Liquid filter during rendering of the first page
    /// to register virtual pages 2..count into OutputReferences. Thread-safe.
    /// </summary>
    internal void RegisterVirtualPagesFromFilter(Page sourcePage, int count)
    {
        if (sourcePage.OutputFormatObj.NoUgly) return;
        var pager = sourcePage.Paginator;
        if (pager is null) return;

        var filename = $"{sourcePage.OutputFormatObj.BaseName}.{sourcePage.OutputFormatObj.Extension}";
        SiteOutputVariant siteVariant = (sourcePage.OutputFormat, sourcePage.ContentSource.Language);
        _siteVariants.TryGetValue(siteVariant, out var siteOutput);
        if (siteOutput is null) return;

        for (var i = 2; i <= count; i++)
        {
            var url = new Uri(
                $"{pager.BaseUrl.TrimEnd('/')}/{pager.PaginatePath}/{i}/{filename}",
                UriKind.Relative);
            var virtualPage = new Page(
                sourcePage.ContentSource,
                this,
                siteOutput,
                siteVariant,
                sourcePage.OutputFormats)
            {
                PageIndex = i,
                RelPermalink = url
            };
            OutputReferences.TryAdd(url, virtualPage);
        }
    }

    /// <summary>
    /// Create fake front matter for system-created pages
    /// </summary>
    /// <param name="relativePath"></param>
    /// <param name="title"></param>
    /// <param name="isTaxonomy"></param>
    ContentSource CreateSystemContentSource(
        string relativePath,
        string title,
        bool isTaxonomy = false)
    {
        relativePath = UrlExtension.NormalizeToUnix(relativePath);

        if (!CacheManager.AutomaticContentCache.TryGetValue(relativePath, out var contentSource))
        {
            var directoryDepth = GetDirectoryDepth(relativePath);
            var sectionName = GetFirstDirectory(relativePath);
            var kind = directoryDepth switch
            {
                0 => Kind.home,
                1 => isTaxonomy ? Kind.taxonomy : Kind.section,
                _ => isTaxonomy ? Kind.term : Kind.list
            };

            var frontMatter = new FrontMatter
            {
                Section = directoryDepth == 0 ? "index" : sectionName,
                Title = title,
                Type = kind == Kind.home ? "index" : sectionName,
                Url = relativePath
            };
            contentSource = new ContentSource(AddIndexAtPath(relativePath), frontMatter, string.Empty)
            {
                BundleType = BundleType.branch,
                Kind = kind
            }
                .ScanForResources(this);
            ApplyLanguage(contentSource);
            CacheManager.AutomaticContentCache.TryAdd(relativePath, contentSource);
        }

        return contentSource;
    }

    static string AddIndexAtPath(string? relativePath, bool useBranch = true) =>
        UrlExtension.NormalizeToUnix(Path.Combine(relativePath ?? "",
            useBranch ? IndexBranchFileConst : IndexLeafFileConst));

    void ProcessPageOutput(IPage page, bool overwrite)
    {
        if (OutputReferences.TryGetValue(page.RelPermalink, out var oldOutput) && !overwrite)
        {
            return;
        }

        page.PostProcess(this);

        UpdatePageReferences(page, oldOutput!);
        RegisterPageUrls(page);
    }

    static void UpdatePageReferences(IPage page, IOutput oldOutput)
    {
        if (oldOutput is not IPage oldPage)
        {
            return;
        }

        foreach (var pageOld in oldPage.PagesReferences)
        {
            page.PagesReferences.Add(pageOld);
        }
    }

    void RegisterPageUrls(IPage page)
    {
        foreach (var pageOutput in page.AllOutputUrLs)
        {
            if (!OutputReferences.TryAdd(pageOutput.Key, pageOutput.Value))
            {
                LogDuplicatePermalink(pageOutput.Key, page);
            }
        }
    }

    void LogDuplicatePermalink(Uri permalink, IPage page)
    {
        Logger.Error(
            "Duplicate RelPermalink '{permalink}' from `{file}`. It is already from '{from}'.",
            permalink,
            page.SourceRelativePath,
            (OutputReferences[permalink] as IFile)!.SourceRelativePath
        );
    }

    void ProcessSectionReference(IPage page)
    {
        if (!string.IsNullOrEmpty(page.Section)
            && OutputReferences.TryGetValue(new Uri('/' + page.Section, UriKind.RelativeOrAbsolute), out var output)
            && output is IPage section
            && page.Kind != Kind.section
            && page.Kind != Kind.taxonomy)
        {
            section.PagesReferences.Add(page.RelPermalink);
        }
    }

    static string GetFirstDirectory(string relativePath) => GetDirectories(relativePath).Length > 0
        ? GetDirectories(relativePath)[0]
        : string.Empty;

    static int GetDirectoryDepth(string relativePath) => GetDirectories(relativePath).Length;

    static string[] GetDirectories(string? relativePath) =>
        (relativePath ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);

    void ParseIndexFrontMatter(
        string? directory,
        int level,
        ref ContentSource? parent,
        ref FrontMatter cascade,
        ref List<string> markdownFiles)
    {
        var (indexFiles, isLeaf) = FindIndexFiles(markdownFiles);
        var hasIndex = indexFiles.Count > 0;

        if (hasIndex)
        {
            markdownFiles = [.. markdownFiles.Where(file => !IsIndexFile(file))];
        }

        var primaryFile = hasIndex ? SelectPrimaryIndex(indexFiles) : null;
        var incomingCascade = cascade;

        var contentSource = hasIndex
            ? BuildIndexContentSource(primaryFile!, isLeaf, incomingCascade)
            : CreateContentSourceForLevel(directory, level);

        if (contentSource is null)
        {
            return;
        }

        if (hasIndex)
        {
            cascade = contentSource.FrontMatter.Cascade ?? cascade;
        }

        contentSource.ContentSourceParent = parent;
        parent?.Children.Add(contentSource);

        SetContentSourceProperties(contentSource, level, hasIndex, ref parent);
        contentSource.ScanForResources(this);
        ContentSourceAdd(contentSource);

        if (!hasIndex)
        {
            if (IsMultilingual)
            {
                CreateSystemContentSourcesForOtherLanguages(contentSource);
            }

            return;
        }

        foreach (var translationFile in indexFiles.Where(file => file != primaryFile))
        {
            var translation = BuildIndexContentSource(translationFile, isLeaf, incomingCascade);
            if (translation is null)
            {
                continue;
            }

            translation.Kind = contentSource.Kind;
            translation.Type = contentSource.Type;
            translation.FrontMatter.Url = string.IsNullOrEmpty(translation.FrontMatter.Url)
                ? contentSource.FrontMatter.Url
                : translation.FrontMatter.Url;
            translation.ContentSourceParent = contentSource.ContentSourceParent;
            contentSource.ContentSourceParent?.Children.Add(translation);
            translation.ScanForResources(this);
            ContentSourceAdd(translation);
        }
    }

    /// <summary>
    /// Finds the index files in a directory. Leaf bundle indexes take precedence over
    /// branch bundle indexes; the returned list contains all language variants of the
    /// selected bundle type.
    /// </summary>
    (List<string> indexFiles, bool isLeaf) FindIndexFiles(List<string> markdownFiles)
    {
        var leafIndexes = markdownFiles.Where(file => LogicalFileName(file) == "index").ToList();
        if (leafIndexes.Count > 0)
        {
            return (leafIndexes, true);
        }

        var branchIndexes = markdownFiles.Where(file => LogicalFileName(file) == "_index").ToList();
        return (branchIndexes, false);
    }

    bool IsIndexFile(string file)
    {
        var logical = LogicalFileName(file);
        return logical is "index" or "_index";
    }

    string SelectPrimaryIndex(List<string> indexFiles) =>
        indexFiles.FirstOrDefault(file => DetectLanguage(Path.GetRelativePath(SourceContentPath, file)).language
            .Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase)) ?? indexFiles[0];

    /// <summary>
    /// The logical filename without extension and without a recognized language suffix.
    /// </summary>
    string LogicalFileName(string filePath)
    {
        var nameNoExtension = Path.GetFileNameWithoutExtension(filePath);
        var lastDot = nameNoExtension.LastIndexOf('.');
        return lastDot > 0 && IsConfiguredLanguage(nameNoExtension[(lastDot + 1)..])
            ? nameNoExtension[..lastDot]
            : nameNoExtension;
    }

    ContentSource? BuildIndexContentSource(string file, bool isLeaf, FrontMatter? cascade = null)
    {
        var fileRelativePath = Path.GetRelativePath(SourceContentPath, file);
        var (frontMatter, rawContent) = ParseFile(file, cascade);

        if (frontMatter is null)
        {
            return null;
        }

        var contentSource = new ContentSource(fileRelativePath, frontMatter, rawContent)
        {
            BundleType = isLeaf ? BundleType.leaf : BundleType.branch
        };

        _ = Interlocked.Increment(ref _filesParsedToReport);

        return contentSource;
    }

    ContentSource? CreateContentSourceForLevel(string? directory, int level)
    {
        switch (level)
        {
            case 0:
                return CreateSystemContentSource(string.Empty, Title);
            case 1:
                {
                    var section = new DirectoryInfo(directory!).Name;
                    return CreateSystemContentSource(section, section);
                }
            default:
                return null;
        }
    }

    void CreateSystemContentSourcesForOtherLanguages(ContentSource primaryCs)
    {
        var sourcePath = primaryCs.SourceRelativePath;
        var nameNoExt = Path.GetFileNameWithoutExtension(sourcePath);
        var ext = Path.GetExtension(sourcePath);

        foreach (var lang in LanguageList.Where(l => !l.IsDefault))
        {
            var langPath = UrlExtension.NormalizeToUnix(
                $"{nameNoExt}.{lang.Code}{ext}");
            var dir = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrEmpty(dir))
            {
                langPath = UrlExtension.NormalizeToUnix($"{dir}/{nameNoExt}.{lang.Code}{ext}");
            }

            if (_contentSources.ContainsKey(langPath))
            {
                continue;
            }

            var langCs = new ContentSource(langPath, new FrontMatter
            {
                Section = primaryCs.Section,
                Title = primaryCs.Title,
                Type = primaryCs.Type,
                Url = primaryCs.Url
            }, string.Empty)
            {
                BundleType = primaryCs.BundleType,
                Kind = primaryCs.Kind
            };
            langCs.ContentSourceParent = primaryCs.ContentSourceParent;
            primaryCs.ContentSourceParent?.Children.Add(langCs);
            langCs.ScanForResources(this);
            ContentSourceAdd(langCs);
        }
    }

    static void SetContentSourceProperties(ContentSource contentSource, int level, bool hasIndex,
        ref ContentSource? parent)
    {
        switch (level)
        {
            case 0:
                contentSource.Kind = Kind.home;
                contentSource.FrontMatter.Url = "/";
                break;
            case 1:
                contentSource.Kind = hasIndex ? contentSource.Kind : Kind.section;
                contentSource.Type ??= "section";
                parent = contentSource;
                break;
            default:
                parent = contentSource;
                break;
        }
    }

    /// <summary>
    /// Detects the language of a content file from its filename suffix
    /// (e.g. <c>hello.pt-br.md</c> → language <c>pt-br</c>, logical name <c>hello</c>).
    /// Only suffixes that match a configured language are recognized.
    /// </summary>
    (string language, string logicalName, string translationKey) DetectLanguage(string sourceRelativePath)
    {
        var directory = UrlExtension.NormalizeToUnix(Path.GetDirectoryName(sourceRelativePath) ?? string.Empty);
        var nameNoExtension = Path.GetFileNameWithoutExtension(sourceRelativePath);

        var language = DefaultLanguage;
        var logicalName = nameNoExtension;

        var lastDot = nameNoExtension.LastIndexOf('.');
        if (lastDot > 0)
        {
            var candidate = nameNoExtension[(lastDot + 1)..];
            if (IsConfiguredLanguage(candidate))
            {
                language = candidate;
                logicalName = nameNoExtension[..lastDot];
            }
        }

        var translationKey = string.IsNullOrEmpty(directory) ? logicalName : $"{directory}/{logicalName}";
        return (language, logicalName, translationKey);
    }

    /// <summary>
    /// Fills the language-related fields of a content source based on its source path.
    /// </summary>
    void ApplyLanguage(ContentSource contentSource)
    {
        var (language, logicalName, translationKey) = DetectLanguage(contentSource.SourceRelativePath);
        contentSource.Language = language;
        contentSource.LogicalFileNameWithoutExtension = logicalName;
        contentSource.TranslationKey = translationKey;
    }

    /// <summary>
    /// Groups content sources that share a translation key, so each page can enumerate
    /// its translations. Must be called after all source files have been scanned.
    /// </summary>
    public void BuildTranslationGroups()
    {
        foreach (var group in _contentSources.Values
                     .GroupBy(cs => cs.TranslationKey, StringComparer.OrdinalIgnoreCase))
        {
            var members = group
                .OrderBy(cs => cs.Language.Equals(DefaultLanguage, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(cs => cs.Language, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var contentSource in members)
            {
                contentSource.Translations.Clear();
                contentSource.Translations.AddRange(members);
            }
        }
    }

    /// <inheritdoc />
    public ContentSource? ContentSourceAdd(ContentSource? contentSource)
    {
        if (contentSource is null)
        {
            return null;
        }

        ApplyLanguage(contentSource);

        if (!_contentSources.TryAdd(contentSource.SourceRelativePath, contentSource))
        {
            Logger.Error("Duplicate front matter found : {filepath}", contentSource.SourceRelativePath);
        }

        var sectionPath1 = AddIndexAtPath(contentSource.Section);
        var sectionPath2 = AddIndexAtPath(contentSource.Section, useBranch: false);
        if (!string.IsNullOrEmpty(contentSource.Section) && (
                _contentSources.TryGetValue(sectionPath2, out var section)
                || _contentSources.TryGetValue(sectionPath1, out section)))
        {
            LinkContent(contentSource, section, false);

            LinkToLangSection(contentSource, section);
        }

        GenerateTags(contentSource);
        return contentSource;
    }

    (FrontMatter?, string) ParseFile(in string fileFullPath, FrontMatter? cascade)
    {
        var fileRelativePath =
            Path.GetRelativePath(SourceContentPath, fileFullPath);
        try
        {
            var fileContent = File.ReadAllText(fileFullPath);
            var (frontMatter, rawContent) = FrontMatter.Parse(fileFullPath, fileRelativePath, Parser, fileContent);

            return (cascade is not null ? cascade.Merge(frontMatter) : frontMatter, rawContent);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error parsing file {file}", fileFullPath);
        }

        return (null, string.Empty);
    }

    // TODO: taxonomy should be customizable
    void GenerateTags(ContentSource contentSource)
    {
        if (contentSource.Tags == null)
        {
            return;
        }

        var basePath = "tags";

        if (!_contentSources.TryGetValue(Path.Combine(basePath, "_index.md"), out var tagSection))
        {
            tagSection = CreateSystemContentSource(basePath, "Tags");
            if (!_contentSources.TryAdd(tagSection.SourceRelativePath, tagSection))
            {
                Log.Error("already exist!");
            }

            if (IsMultilingual)
            {
                CreateSystemContentSourcesForOtherLanguages(tagSection);
            }
        }

        LinkContent(contentSource, tagSection, false);
        LinkToLangSection(contentSource, tagSection);

        foreach (var tag in contentSource.Tags)
        {
            var path = AddIndexAtPath(Path.Combine(basePath, tag));
            if (!_contentSources.TryGetValue(path, out var tagContentSource))
            {
                tagContentSource = CreateSystemContentSource(Path.Combine(basePath, tag), tag);
                tagContentSource.ContentSourceParent = tagSection;
                _contentSources.TryAdd(tagContentSource.SourceRelativePath, tagContentSource);

                if (IsMultilingual)
                {
                    CreateSystemContentSourcesForOtherLanguages(tagContentSource);
                }
            }

            LinkContent(contentSource, tagContentSource, true);
            LinkToLangSection(contentSource, tagContentSource);
        }
    }

    /// <summary>
    /// If multilingual, also links a non-default-language content source to the
    /// language-specific section/taxonomy variant, so that <c>Pages</c> on the
    /// non-default section includes translated children.
    /// </summary>
    void LinkToLangSection(ContentSource contentSource, ContentSource section)
    {
        if (!IsMultilingual || string.Equals(contentSource.Language, DefaultLanguage,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var langSectionPath = LanguageSuffixedPath(section.SourceRelativePath, contentSource.Language);
        if (_contentSources.TryGetValue(langSectionPath, out var langSection))
        {
            LinkContent(contentSource, langSection, false);
        }
    }

    /// <summary>
    /// Inserts a language suffix before the extension of a source relative path.
    /// e.g. <c>blog/_index.md</c> + <c>pt-br</c> → <c>blog/_index.pt-br.md</c>
    /// </summary>
    static string LanguageSuffixedPath(string sourceRelativePath, string language)
    {
        var dir = Path.GetDirectoryName(sourceRelativePath);
        var nameNoExt = Path.GetFileNameWithoutExtension(sourceRelativePath);
        var ext = Path.GetExtension(sourceRelativePath);
        var path = string.IsNullOrEmpty(dir)
            ? $"{nameNoExt}.{language}{ext}"
            : $"{dir}/{nameNoExt}.{language}{ext}";
        return UrlExtension.NormalizeToUnix(path);
    }

    static void LinkContent(ContentSource content, ContentSource parent,
        bool isTag)
    {
        if (isTag)
        {
            content.ContentSourceTags.Add(parent);
        }

        parent.Children.Add(content);
    }

    /// <summary>
    /// Return a list of all output formats for a given Kind
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="kindOutputFormats"></param>
    /// <returns></returns>
    static List<string> GetUniqueOutputFormats(Kind kind,
        Dictionary<Kind, List<string>> kindOutputFormats)
        => [.. kindOutputFormats
            .Where(kvp => (kind & kvp.Key) == kvp.Key)
            .SelectMany(kvp => kvp.Value)
            .Distinct()];

    public string ParseAndRenderTemplate(Page page, bool isBaseTemplate)
    {
        var templatePath = FileUtils.GetTemplatePath(this, page, isBaseTemplate);

        try
        {
            // For output formats with a built-in template (e.g. sitemap), only honour a theme
            // template when it is format-specific (filename contains the output format name).
            // This prevents generic templates like _default/list.xml or _default/baseof.xml
            // from shadowing the built-in. Theme authors override by adding _default/sitemap.xml.
            var builtinForFormat = FileUtils.GetBuiltinTemplate(page);
            if (!string.IsNullOrEmpty(builtinForFormat))
            {
                var isFormatSpecificThemeTemplate = !string.IsNullOrEmpty(templatePath)
                                                    && Path.GetFileNameWithoutExtension(templatePath)
                                                        .Contains(page.OutputFormat,
                                                            StringComparison.OrdinalIgnoreCase);
                if (isFormatSpecificThemeTemplate)
                {
                    return TemplateEngine.Render(templatePath, this, page);
                }

                // Base-template pass: return content already rendered by the non-base pass.
                // Content-template pass: render the built-in.
                return isBaseTemplate ? page.Content : TemplateEngine.RenderInline(builtinForFormat, this, page);
            }

            if (!string.IsNullOrEmpty(templatePath))
            {
                return TemplateEngine.Render(templatePath, this, page);
            }

            var inlineTemplate = FileUtils.GetTemplate(this, page, isBaseTemplate);
            if (!string.IsNullOrEmpty(inlineTemplate))
            {
                return TemplateEngine.RenderInline(inlineTemplate, this, page);
            }

            return isBaseTemplate ? page.Content : page.ContentPreRendered;
        }
        catch (FormatException ex)
        {
            Logger.Error(ex, "Error rendering theme template: {templatePath}", templatePath);
            return string.Empty;
        }
    }
}
