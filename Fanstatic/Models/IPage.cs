using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Markdig;
using Fanstatic.Helpers;

namespace Fanstatic.Models;

/// <summary>
/// Each page data created from source files or from the system.
/// </summary>
public interface IPage : IOutput, IFile, IContentSource
{
    /// <summary>
    /// The underlining content source
    /// </summary>
    ContentSource ContentSource { get; }

    /// <summary>
    /// The source directory of the file.
    /// </summary>
    string? SourcePathLastDirectory => string.IsNullOrEmpty(ContentSource.SourceRelativePathDirectory)
        ? null
        : Path.GetFileName(Path.GetFullPath(
            ContentSource.SourceRelativePathDirectory.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)));

    /// <summary>
    /// Secondary URL patterns to be used to create the url.
    /// </summary>
    Collection<Uri>? AliasesProcessed { get; }

    /// <summary>
    /// Other content that mention this content.
    /// Used to create the tags list and Related Posts section.
    /// </summary>
    ConcurrentBag<Uri> PagesReferences { get; }

    /// <summary>
    /// Other content that mention this content.
    /// Used to create the tags list and Related Posts section.
    /// </summary>
    IPage? Parent { get; }

    /// <summary>
    /// Plain markdown content, without HTML.
    /// </summary>
    string Plain => Markdown.ToPlainText(ContentSource.RawContent, SiteHelper.MarkdownPipeline);

    /// <summary>
    /// A list of tags, if any.
    /// </summary>
    List<IPage> TagsReference { get; }

    /// <summary>
    /// Just a simple check if the current page is the home page
    /// </summary>
    bool IsHome => Site.Home == this;

    /// <summary>
    /// Just a simple check if the current page is a "page"
    /// </summary>
    bool IsPage => (Kind & Kind.single) == Kind.single && (Kind & Kind.system) != Kind.system;

    /// <summary>
    /// Just a simple check if the current page is a section page
    /// </summary>
    bool IsSection => Type == "section";

    /// <summary>
    /// The number of words in the main content
    /// </summary>
    int WordCount => Plain.Split(NonWords, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>
    /// Characters that are not considered as words
    /// </summary>
    protected static readonly char[] NonWords = [' ', ',', ';', '.', '!', '"', '(', ')', '?', '\n', '\r'];

    /// <summary>
    /// The markdown content converted to HTML
    /// </summary>
    string ContentPreRendered { get; }

    /// <summary>
    /// The processed content.
    /// </summary>
    string Content { get; }

    /// <summary>
    /// Creates the output file by applying the theme templates to the page content.
    /// </summary>
    /// <returns>The processed output file content.</returns>
    string CompleteContent { get; }

    /// <summary>
    /// The output format used
    /// </summary>
    string OutputFormat { get; set; }

    /// <summary>
    /// All output formats of the content has
    /// </summary>
    List<string> OutputFormats { get; }

    /// <summary>
    /// The language of this page.
    /// </summary>
    LanguageSettings Language { get; }

    /// <summary>
    /// True when this page is in the site's default language.
    /// </summary>
    bool IsDefaultLanguage => Language.IsDefault;

    /// <summary>
    /// All translations of this page, including itself, in the same output format,
    /// sorted by language weight. Useful for language switchers.
    /// </summary>
    IEnumerable<IPage> AllTranslations { get; }

    /// <summary>
    /// The translations of this page in other languages (excluding itself), in the same
    /// output format.
    /// </summary>
    IEnumerable<IPage> Translations { get; }

    /// <summary>
    /// This page's translations keyed by language code (including itself), in the same
    /// output format. Handy for building a switcher over <c>site.Languages</c>: look up
    /// each language code and fall back to the language home when it is missing.
    /// </summary>
    IReadOnlyDictionary<string, IPage> TranslationsByLanguage { get; }

    /// <summary>
    /// The alternate output formats of this page (including itself) in the same language,
    /// e.g. the RSS or JSON representation of an HTML page.
    /// </summary>
    IEnumerable<IPage> AlternativeOutputFormats { get; }

    /// <summary>
    /// Every alternate representation of this logical page across all languages and all
    /// output formats (including itself). The unified variant model.
    /// </summary>
    IEnumerable<IPage> Variants { get; }

    /// <summary>
    /// Other content that mention this content.
    /// Used to create the tags list and Related Posts section.
    /// </summary>
    IEnumerable<IPage> Pages { get; }

    /// <summary>
    /// List of pages from the content folder.
    /// </summary>
    IEnumerable<IPage> RegularPages { get; }

    /// <summary>
    /// Get all URLs related to this content.
    /// </summary>
    Dictionary<Uri, IOutput> AllOutputUrLs { get; }

    /// <summary>
    /// Pagination metadata for list pages. Null when the page has fewer items than the paginate threshold.
    /// </summary>
    Pager? Paginator { get; }

    /// <summary>
    /// Final steps of parsing the content.
    /// </summary>
    void PostProcess(ISite site);
}
