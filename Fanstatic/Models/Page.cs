using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Markdig;
using Microsoft.Extensions.FileSystemGlobbing;
using Fanstatic.Helpers;
using Fanstatic.Parsers;

namespace Fanstatic.Models;

/// <summary>
/// Each page data created from source files or from the system.
/// </summary>
public class Page : IPage
{
    #region IPage

    /// <inheritdoc/>
    public ContentSource ContentSource { get; init; }

    /// <inheritdoc/>
    public string? SourcePathLastDirectory => string.IsNullOrEmpty(SourceRelativePathDirectory)
        ? null
        : Path.GetFileName(Path.GetFullPath(
            SourceRelativePathDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

    /// <inheritdoc/>
    public ISiteOutput Site { get; init; }

    /// <inheritdoc/>
    public ISite SiteInternal { get; init; }

    /// <inheritdoc/>
    public Collection<Uri>? AliasesProcessed { get; private set; }

    /// <inheritdoc/>
    public ConcurrentBag<Uri> PagesReferences { get; } = [];

    /// <inheritdoc/>
    public IPage? Parent
    {
        get
        {
            var parentContentSource = ContentSourceParent;
            if (parentContentSource is null)
            {
                return null;
            }

            // Prefer the parent translation that matches this page's language, so that
            // a translated child inherits the translated (prefixed) parent URL.
            var parentForLanguage = parentContentSource.Translations
                .FirstOrDefault(translation => string.Equals(translation.Language,
                    ContentSource.Language, StringComparison.OrdinalIgnoreCase)) ?? parentContentSource;

            var parentPages = parentForLanguage.ContentSourceToPages;
            if (parentPages.Count == 0)
            {
                return null;
            }

            return parentPages.FirstOrDefault(page => page.OutputFormat == OutputFormat)
                   ?? parentPages[0];
        }
    }

    /// <inheritdoc/>
    public string Plain =>
        Markdown.ToPlainText(RawContent, SiteHelper.MarkdownPipeline);

    /// <inheritdoc/>
    // TODO:
    public List<IPage> TagsReference
    {
        get
        {
            List<IPage> tagsReferences = [];
            foreach (var tag in ContentSourceTags)
            {
                tagsReferences.AddRange(tag.ContentSourceToPages
                    .Where(page => page.OutputFormat == OutputFormat));
            }

            return tagsReferences;
        }
    }

    /// <inheritdoc/>
    public bool IsHome => Site.Home == this;

    /// <inheritdoc/>
    public bool IsPage => Kind == Kind.single;

    /// <inheritdoc/>
    public bool IsSection => Type == "section";

    /// <inheritdoc/>
    public int WordCount => Plain
        .Split(IPage.NonWords,
            StringSplitOptions.RemoveEmptyEntries).Length;

    /// <inheritdoc/>
    public string ContentPreRendered => _contentPreRenderedCached.Value;

    /// <inheritdoc/>
    public string Content => SiteInternal.ParseAndRenderTemplate(this, false);

    /// <inheritdoc/>
    public string CompleteContent => SiteInternal.ParseAndRenderTemplate(this, true);

    /// <inheritdoc/>
    public string OutputFormat { get; set; }

    /// <inheritdoc/>
    public List<string> OutputFormats { get; set; }

    /// <inheritdoc/>
    public LanguageSettings Language => Site.Language;

    /// <inheritdoc/>
    public bool IsDefaultLanguage => Language.IsDefault;

    /// <inheritdoc/>
    public IEnumerable<IPage> AllTranslations
    {
        get
        {
            field ??= ContentSource.Translations
                .SelectMany(translation => translation.ContentSourceToPages)
                .Where(page => page.OutputFormat == OutputFormat)
                .OrderBy(page => page.Language.Weight)
                .ThenBy(page => page.Language.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return field;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<IPage> Translations =>
        AllTranslations.Where(page => !ReferenceEquals(page, this));

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, IPage> TranslationsByLanguage
    {
        get
        {
            field ??= AllTranslations
                .GroupBy(page => page.Language.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            return field;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<IPage> AlternativeOutputFormats =>
        ContentSource.ContentSourceToPages;

    /// <inheritdoc/>
    public IEnumerable<IPage> Variants
    {
        get
        {
            field ??= ContentSource.Translations
                .SelectMany(translation => translation.ContentSourceToPages)
                .ToList();
            return field;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<IPage> Pages
    {
        get
        {
            field ??= ContentSource.Children
                .SelectMany(child => child.ContentSourceToPages)
                .Where(page => page.OutputFormat == OutputFormat
                               && string.Equals(page.ContentSource.Language,
                                   ContentSource.Language, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return field;
        }
    }

    /// <inheritdoc/>
    public IEnumerable<IPage> RegularPages
    {
        get
        {
            field ??= Pages
                .Where(page => page.IsPage);
            return field;
        }
    }

    /// <inheritdoc/>
    public Dictionary<Uri, IOutput> AllOutputUrLs => _allOutputUrLs;

    /// <summary>
    /// Which pagination page this instance represents (1-based). Page 1 is the original;
    /// pages 2+ are virtual instances created by RegisterPaginatedUrls.
    /// </summary>
    public int PageIndex { get; set; } = 1;

    /// <inheritdoc/>
    public Pager? Paginator => _paginator;

    Pager? _paginator;

    /// <summary>
    /// Called by the <c>paginate</c> Liquid filter to set the paginator for this page.
    /// </summary>
    internal void SetPaginator(Pager pager) => _paginator = pager;

    Dictionary<Uri, IOutput> BuildAllOutputUrLs()
    {
        var urls = new Dictionary<Uri, IOutput>();

        AddRelPermalink(urls);
        AddAliases(urls);
        AddResources(urls);

        return urls;
    }

    void AddRelPermalink(Dictionary<Uri, IOutput> urls) => urls.TryAdd(RelPermalink, this);

    void AddAliases(Dictionary<Uri, IOutput> urls)
    {
        if (AliasesProcessed is null) return;
        foreach (var alias in AliasesProcessed)
        {
            if (!urls.ContainsKey(alias))
            {
                urls.Add(alias, this);
            }
        }
    }

    void AddResources(Dictionary<Uri, IOutput> urls)
    {
        if (Resources is null) return;
        foreach (var resource in Resources)
        {
            urls.TryAdd(resource.RelPermalink, resource);
        }
    }

    /// <inheritdoc/>
    /// <inheritdoc/>
    public void PostProcess(ISite site)
    {
        ArgumentNullException.ThrowIfNull(site);

        // Create all the aliases
        if (Aliases is not null)
        {
            AliasesProcessed ??= [];
            foreach (var alias in Aliases)
            {
                AliasesProcessed.Add(site.CreatePermalink(this, alias));
            }
        }

        PostProcessResources();
        _allOutputUrLs = BuildAllOutputUrLs();
    }

    #endregion IPage

    #region IFrontMatter

    /// <inheritdoc/>
    public string? Title => ContentSource.Title;

    /// <inheritdoc/>
    public string? Section => ContentSource.Section;

    /// <inheritdoc/>
    public string? Url => ContentSource.Url;

    /// <inheritdoc/>
    public string? UrlTemplate => Url ?? (SourceFileNameWithoutExtension == "index" ? UrlForIndex : UrlForNonIndex);

    /// <inheritdoc/>
    public bool? Draft => ContentSource.Draft;

    /// <inheritdoc/>
    public List<string>? Aliases => ContentSource.Aliases;

    /// <inheritdoc/>
    public DateTime? Date => ContentSource.Date;

    /// <inheritdoc/>
    public DateTime? LastMod => ContentSource.LastMod;

    /// <inheritdoc/>
    public DateTime? PublishDate => ContentSource.PublishDate;

    /// <inheritdoc/>
    public DateTime? ExpiryDate => ContentSource.ExpiryDate;

    /// <inheritdoc/>
    public int Weight => ContentSource.Weight;

    /// <inheritdoc/>
    public List<string>? Tags => ContentSource.Tags;

    /// <inheritdoc/>
    public List<FrontMatterResources>? ResourceDefinitions => ContentSource.ResourceDefinitions;

    /// <inheritdoc/>
    public string RawContent => ContentSource.RawContent;

    /// <inheritdoc/>
    public List<IPage> ContentSourceToPages => ContentSource.ContentSourceToPages;

    /// <inheritdoc/>
    public ContentSource? ContentSourceParent => ContentSource.ContentSourceParent;

    /// <inheritdoc/>
    public string SourceRelativePath => ContentSource.SourceRelativePath;

    /// <inheritdoc/>
    public string SourceRelativePathDirectory => ContentSource.SourceRelativePathDirectory;

    /// <inheritdoc/>
    public string? SourceFileNameWithoutExtension => (ContentSource as IFile).SourceFileNameWithoutExtension;

    #endregion IFrontMatter

    #region IContentSource

    /// <inheritdoc/>
    public string? Type => ContentSource.Type;

    /// <inheritdoc/>
    public Kind Kind => ContentSource.Kind;

    /// <inheritdoc/>
    public BundleType BundleType => ContentSource.BundleType;

    /// <inheritdoc/>
    public List<ContentSource> ContentSourceTags =>
        ContentSource.ContentSourceTags;

    #endregion IContentSource

    #region IParams

    /// <inheritdoc/>
    public Dictionary<string, object> Params
    {
        get => ContentSource.Params;
        set => ContentSource.Params = value;
    }

    #endregion IParams

    #region IOutput

    /// <inheritdoc/>
    public Uri RelPermalink { get; set; } = new Uri("", UriKind.RelativeOrAbsolute);

    #endregion IOutput

    /// <summary>
    /// List of attached resources
    /// </summary>
    // TODO: why is this public?
    public List<Resource>? Resources { get; set; }

    /// <summary>
    /// The actual object with OutputFormat data
    /// </summary>
    // TODO: why is this public?
    public OutputFormat OutputFormatObj { get; }

    /// <summary>
    /// The markdown content.
    /// </summary>
    readonly Lazy<string> _contentPreRenderedCached;

    const string UrlForIndex = @"{%- liquid
if page.Parent
echo page.Parent.RelPermalinkDir
echo '/'
endif
if page.Title != ''
echo page.Title
else
echo page.SourcePathLastDirectory
endif
-%}";

    const string UrlForNonIndex = @"{%- liquid
if page.Parent
echo page.Parent.RelPermalinkDir
echo '/'
endif
if page.Title != ''
echo page.Title
else
echo page.SourceFileNameWithoutExtension
endif
-%}";

    Dictionary<Uri, IOutput> _allOutputUrLs = new();

    /// <summary>
    /// Constructor
    /// </summary>
    public Page(in ContentSource contentSource, ISite siteInternal, in ISiteOutput site,
        SiteOutputVariant siteVariant, List<string> outputFormats)
    {
        ContentSource = contentSource;
        Site = site;
        SiteInternal = siteInternal;
        OutputFormat = siteVariant.outputFormat;
        OutputFormats = outputFormats;

        FileUtils.OutputFormats.TryGetValue(OutputFormat, out var outputFormatObj);

        OutputFormatObj = outputFormatObj ??
                          throw new ArgumentException("No output format for {OutputFormat}", OutputFormat);

        _allOutputUrLs = BuildAllOutputUrLs();
        _contentPreRenderedCached = new(() =>
        {
            var content = RefShortcodeParser.Process(RawContent, SiteInternal, this);
            return Markdown.ToHtml(content, SiteHelper.MarkdownPipeline);
        });
    }

    /// <summary>
    /// Process resources for this page, generating permalinks
    /// </summary>
    void PostProcessResources()
    {
        if (ContentSource.RawResources?.Any() != true)
        {
            return;
        }

        Resources = [.. ProcessResourcesWithDefinitions()];
    }

    IEnumerable<Resource> ProcessResourcesWithDefinitions()
    {
        var counter = 0;
        return ContentSource.RawResources!
            .Where(resource => resource.Resource == null)
            .Select(sourceResource => CreateResourceWithCustomization(sourceResource, ref counter));
    }

    Resource CreateResourceWithCustomization(ContentSourceResource sourceResource, ref int counter)
    {
        var filenameOriginal = Path.GetFileName(sourceResource.SourceRelativePath);
        var extension = Path.GetExtension(sourceResource.SourceRelativePath);

        var resourceCustomization = GetResourceCustomization(filenameOriginal, ref counter);

        var filename = resourceCustomization.Filename ?? filenameOriginal;
        filename = Path.GetFileNameWithoutExtension(filename) + extension;

        // RelPermalinkDir has no trailing slash (e.g. "/my-post", or "/" for the
        // root), so add one before appending the resource filename.
        var permalinkDir = (this as IOutput).RelPermalinkDir.ToString();
        if (!permalinkDir.EndsWith('/'))
        {
            permalinkDir += "/";
        }

        var resource = new Resource
        {
            Title = resourceCustomization.Title ?? filenameOriginal,
            Params = resourceCustomization.Params ?? sourceResource.Params,
            SourceRelativePath = sourceResource.SourceRelativePath,
            Site = Site,
            RelPermalink = new Uri(permalinkDir + filename, UriKind.RelativeOrAbsolute)
        };
        sourceResource.Resource = resource;
        return resource;
    }

    (string? Filename, string? Title, Dictionary<string, object>? Params
        ) GetResourceCustomization(string filenameOriginal, ref int counter)
    {
        // Early return if no resource definitions
        if (ResourceDefinitions == null)
        {
            return (null, null, null);
        }

        // Find the first matching resource definition
        var matchedDefinition = ResourceDefinitions
            .FirstOrDefault(resourceDefinition =>
            {
                resourceDefinition.GlobMatcher ??= new();
                _ = resourceDefinition.GlobMatcher.AddInclude(resourceDefinition.Src);

                var file = new InMemoryDirectoryInfo("./", [filenameOriginal]);
                return resourceDefinition.GlobMatcher.Execute(file).HasMatches;
            });

        // If no match found, return null
        if (matchedDefinition == null)
        {
            return (null, null, null);
        }

        // SiteInternal is the full ISite (with the template engine). `Site` is only
        // the ISiteOutput facade, so it cannot render Name/Title templates.
        var site = SiteInternal;

        // Process matched definition
        var filename = string.IsNullOrEmpty(matchedDefinition.Name)
            ? filenameOriginal
            : site.TemplateEngine.Render(matchedDefinition.Name, site, this, counter);

        var title = string.IsNullOrEmpty(matchedDefinition.Title)
            ? filenameOriginal
            : site.TemplateEngine.Render(matchedDefinition.Title, site, this, counter);

        counter++;

        return (filename, title, matchedDefinition.Params);
    }
}
