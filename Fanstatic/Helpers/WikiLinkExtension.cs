using Fanstatic.Parsers;
using Markdig;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Fanstatic.Helpers;

/// <summary>
/// A Markdig extension recognizing Obsidian-style wikilinks: <c>[[target]]</c> and <c>[[target|label]]</c>.
/// The target is resolved to a URL by <see cref="WikiLinkResolver"/> against the ambient
/// <see cref="WikiLinkContext"/>. An unresolved target renders <c>./</c> and logs an error, unless
/// <c>MissingWikiLinkAsError</c> is set, in which case the build fails.
/// </summary>
public sealed class WikiLinkExtension : IMarkdownExtension
{
    /// <inheritdoc/>
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        if (!pipeline.InlineParsers.Contains<WikiLinkParser>())
        {
            pipeline.InlineParsers.InsertBefore<LinkInlineParser>(new WikiLinkParser());
            pipeline.DocumentProcessed += RemoveNestedLinks;
        }
    }

    /// <inheritdoc/>
    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
    }

    static void RemoveNestedLinks(MarkdownDocument document)
    {
        foreach (var link in document.Descendants<WikiLinkInline>().ToArray())
        {
            for (var parent = link.Parent; parent is not null; parent = parent.Parent)
            {
                if (parent is not LinkInline { IsImage: false })
                {
                    continue;
                }

                var label = link.FirstChild!;
                label.Remove();
                link.ReplaceBy(label);
                break;
            }
        }
    }
}

/// <summary>
/// Identifies a wikilink for label normalization after ordinary links have been parsed.
/// </summary>
sealed class WikiLinkInline : LinkInline;

/// <summary>
/// Matches <c>[[...]]</c> and produces a standard link with a URL resolved during HTML rendering.
/// </summary>
sealed class WikiLinkParser : InlineParser
{
    /// <summary>Creates the parser and declares <c>[</c> as its opening character.</summary>
    public WikiLinkParser()
    {
        OpeningCharacters = ['['];
    }

    /// <inheritdoc/>
    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        var start = slice.Start;
        var text = slice.Text;

        if (slice.PeekCharExtra(1) != '[')
        {
            return false;
        }

        var closing = FindClose(slice);
        if (closing < 0)
        {
            return false;
        }

        var link = CreateLink(text[(start + 2)..closing]);
        if (link is null)
        {
            return false;
        }

        processor.Inline = link;
        slice.Start = closing + 2;
        return true;
    }

    static LinkInline? CreateLink(string inner)
    {
        var pipe = inner.IndexOf('|');
        var targetEnd = pipe > 0 && inner[pipe - 1] == '\\' ? pipe - 1 : pipe;
        var target = (pipe < 0 ? inner : inner[..targetEnd]).Trim();
        var label = pipe < 0 ? string.Empty : inner[(pipe + 1)..].Trim();
        if (target.Length == 0)
        {
            return null;
        }

        var link = new WikiLinkInline
        {
            IsClosed = true,
            GetDynamicUrl = () => WikiLinkContext.CurrentScope is { } scope
                ? WikiLinkResolver.Resolve(scope.Site, scope.Page, target)
                : "./"
        };
        link.AppendChild(new LiteralInline(string.IsNullOrEmpty(label) ? target : label));
        return link;
    }

    static int FindClose(StringSlice slice)
    {
        var text = slice.Text;
        for (var i = slice.Start + 2; i < slice.End; i++)
        {
            if (text[i] == ']' && i + 1 <= slice.End && text[i + 1] == ']')
            {
                return i;
            }

            if (text[i] == '\n')
            {
                return -1;
            }
        }

        return -1;
    }
}
