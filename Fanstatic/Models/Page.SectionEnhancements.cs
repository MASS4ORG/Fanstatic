using System.Text;
using Fanstatic.Helpers;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Fanstatic.Models;

public partial class Page
{
    const string MoreMarker = "<!--more-->";

    const int WordsPerMinute = 213;

    static readonly char[] SummaryWordSeparators = [' ', ',', ';', '.', '!', '"', '(', ')', '?', '\n', '\r'];

    Lazy<IReadOnlyList<IPage>>? _ancestorsCached;

    Lazy<IPage?>? _firstSectionCached;

    Lazy<IReadOnlyList<IPage>>? _sectionsCached;

    Lazy<(IPage? Prev, IPage? Next)>? _neighborsCached;

    Lazy<(IPage? Prev, IPage? Next)>? _sectionNeighborsCached;

    Lazy<string>? _summaryCached;

    Lazy<bool>? _truncatedCached;

    Lazy<int>? _readingTimeCached;

    Lazy<PageFragments>? _fragmentsCached;

    Lazy<string>? _tableOfContentsCached;

    /// <inheritdoc/>
    public IReadOnlyList<IPage> Ancestors => _ancestorsCached!.Value;

    /// <inheritdoc/>
    public IPage? FirstSection => _firstSectionCached!.Value;

    /// <inheritdoc/>
    public IReadOnlyList<IPage> Sections => _sectionsCached!.Value;

    /// <inheritdoc/>
    public IPage? Prev => _neighborsCached!.Value.Prev;

    /// <inheritdoc/>
    public IPage? Next => _neighborsCached!.Value.Next;

    /// <inheritdoc/>
    public IPage? PrevInSection => _sectionNeighborsCached!.Value.Prev;

    /// <inheritdoc/>
    public IPage? NextInSection => _sectionNeighborsCached!.Value.Next;

    /// <inheritdoc/>
    public string Summary => _summaryCached!.Value;

    /// <inheritdoc/>
    public bool Truncated => _truncatedCached!.Value;

    /// <inheritdoc/>
    public int ReadingTime => _readingTimeCached!.Value;

    /// <inheritdoc/>
    public PageFragments Fragments => _fragmentsCached!.Value;

    /// <inheritdoc/>
    public string TableOfContents => _tableOfContentsCached!.Value;

    void InitializeSectionEnhancements()
    {
        _ancestorsCached = new(CreateAncestors);
        _firstSectionCached = new(CreateFirstSection);
        _sectionsCached = new(CreateSections);
        _neighborsCached = new(() => FindNeighbors(GetSiteNeighbors()));
        _sectionNeighborsCached = new(() => FindNeighbors(GetSectionNeighbors()));
        _summaryCached = new(CreateSummary);
        _truncatedCached = new(() => Summary.Length < Plain.Length);
        _readingTimeCached = new(() => (int)Math.Ceiling(WordCount / (double)WordsPerMinute));
        _fragmentsCached = new(CreateFragments);
        _tableOfContentsCached = new(() =>
        {
            var levels = SiteInternal.TableOfContents ?? new TableOfContentsSettings();
            return TableOfContentsRenderer.Render(Fragments.Headings, levels.StartLevel, levels.EndLevel);
        });
    }

    /// <summary>
    /// Raw Markdown with the summary separator removed before conversion.
    /// </summary>
    string MarkdownForRendering => RawContent.Replace(MoreMarker, string.Empty, StringComparison.OrdinalIgnoreCase);

    IReadOnlyList<IPage> CreateAncestors()
    {
        List<IPage> ancestors = [];
        var seen = new HashSet<IPage>(ReferenceEqualityComparer.Instance) { this };
        var current = Parent;
        while (current is not null && seen.Add(current))
        {
            ancestors.Add(current);
            current = current.Parent;
        }

        return ancestors;
    }

    IPage? CreateFirstSection()
    {
        if (Kind == Kind.section)
        {
            return this;
        }

        return Ancestors.LastOrDefault(static ancestor => ancestor.Kind == Kind.section);
    }

    IReadOnlyList<IPage> CreateSections() =>
        Pages.Where(page => !ReferenceEquals(page, this) && page.Kind is Kind.section or Kind.list).ToList();

    IReadOnlyList<IPage> GetSiteNeighbors() => Site.RegularPagesByDateDescendingTitle;

    IReadOnlyList<IPage> GetSectionNeighbors()
    {
        var section = FirstSection;
        return section is null ? [] : OrderByDateDescendingThenTitle(section.RegularPagesByDate);
    }

    static IReadOnlyList<IPage> OrderByDateDescendingThenTitle(IReadOnlyList<IPage> orderedByDate) =>
        orderedByDate
            .GroupBy(static page => page.Date)
            .OrderByDescending(static group => group.Key)
            .SelectMany(static group => group.OrderBy(static page => page.Title))
            .ToList();

    (IPage? Prev, IPage? Next) FindNeighbors(IReadOnlyList<IPage> ordered)
    {
        for (var index = 0; index < ordered.Count; index++)
        {
            if (!ReferenceEquals(ordered[index], this))
            {
                continue;
            }

            return (index > 0 ? ordered[index - 1] : null,
                index < ordered.Count - 1 ? ordered[index + 1] : null);
        }

        return (null, null);
    }

    string CreateSummary()
    {
        if (!string.IsNullOrEmpty(ContentSource.Summary))
        {
            return ContentSource.Summary;
        }

        var markerIndex = RawContent.IndexOf(MoreMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            var beforeMarker = Markdown.ToPlainText(RawContent[..markerIndex], SiteHelper.MarkdownPipeline);
            return beforeMarker.Trim();
        }

        var words = Plain.Split(SummaryWordSeparators, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Take(Math.Max(0, SiteInternal.SummaryLength)));
    }

    PageFragments CreateFragments()
    {
        var document = Markdown.Parse(MarkdownForRendering, SiteHelper.MarkdownPipeline);
        var headings = document.Descendants().OfType<HeadingBlock>()
            .Select(static heading => new Heading(heading.Level, GetHeadingText(heading),
                heading.TryGetAttributes()?.Id ?? string.Empty))
            .ToList();
        return new PageFragments(headings);
    }

    static string GetHeadingText(HeadingBlock heading)
    {
        if (heading.Inline is null)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        foreach (var leaf in heading.Inline.Descendants().OfType<LeafInline>())
        {
            if (leaf is LiteralInline literal)
            {
                _ = text.Append(literal.Content);
            }
            else if (leaf is CodeInline code)
            {
                _ = text.Append(code.Content);
            }
        }

        return text.ToString();
    }
}
