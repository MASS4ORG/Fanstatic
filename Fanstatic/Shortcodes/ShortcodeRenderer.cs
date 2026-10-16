using System.Text;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Markdig;

namespace Fanstatic.Shortcodes;

/// <summary>
/// Renders the shortcodes of a page and converts its Markdown to HTML in Hugo's order: Markdown-notation output is
/// spliced into the Markdown, standard-notation output replaces placeholders after Markdig, and nested calls render
/// inside out so a parent receives the rendered output of its children as <c>inner</c>.
/// </summary>
public static class ShortcodeRenderer
{
    const string PlaceholderPrefix = "FANSTATICSC";

    const char PlaceholderSuffix = 'X';

    /// <summary>
    /// Renders the shortcodes in <paramref name="markdown"/> and converts it to HTML.
    /// </summary>
    /// <exception cref="ShortcodeException">A shortcode is malformed, unknown or fails, located in the content file.</exception>
    public static string ToHtml(string markdown, ISite site, IPage page)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(page);

        if (!MayContainShortcodes(markdown))
        {
            return Markdown.ToHtml(markdown ?? string.Empty, SiteHelper.MarkdownPipeline);
        }

        var state = new RenderState(markdown, site, page);
        try
        {
            var calls = Parse(markdown, site, page);
            List<string> outputs = [];
            var expanded = Splice(state, 0, markdown.Length, calls, call =>
            {
                var output = RenderCall(call, null, state);
                if (call.Literal is not null || call.Notation == ShortcodeNotation.Markdown)
                {
                    return output;
                }

                outputs.Add(output);
                return PlaceholderPrefix + (outputs.Count - 1) + PlaceholderSuffix;
            });

            return ReplacePlaceholders(Markdown.ToHtml(expanded, SiteHelper.MarkdownPipeline), outputs);
        }
        catch (ShortcodeException ex) when (ex.SourcePath.Length == 0)
        {
            throw state.Locate(ex);
        }
    }

    /// <summary>
    /// Renders one shortcode called from a template, with argument values already evaluated.
    /// </summary>
    /// <exception cref="ShortcodeException">The shortcode is unknown or fails.</exception>
    public static string RenderFromTemplate(string name, IReadOnlyList<ShortcodeArgument> arguments, string? inner,
        ISite site, IPage page)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(page);

        try
        {
            var template = site.ShortcodeRegistry.Resolve(name, page)
                           ?? throw new ShortcodeException($"shortcode '{name}' not found", 0);
            var context = ShortcodeBinder.Bind(name, arguments, template, null, 0, null);
            context.Inner = inner;
            return RenderTemplate(template, context, site, page);
        }
        catch (Exception ex) when (ex is not ShortcodeException)
        {
            throw new ShortcodeException($"shortcode '{name}': {ex.Message}", 0, ex);
        }
    }

    /// <summary>
    /// The names of the shortcodes a content calls, sorted, without rendering them.
    /// </summary>
    public static IReadOnlyList<string> CollectNames(string markdown, ISite site, IPage page)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(page);

        if (!MayContainShortcodes(markdown))
        {
            return [];
        }

        try
        {
            return Tokenize(markdown, site, page)
                .Where(token => token.Kind == ShortcodeTokenKind.Open)
                .Select(token => token.Name)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex) when (ex is ShortcodeException or FormatException)
        {
            return [];
        }
    }

    /// <summary>
    /// Builds the call tree of a content, including <c>:::</c> directives that name a shortcode.
    /// </summary>
    /// <exception cref="ShortcodeException">The content holds a malformed, unknown or unbalanced shortcode.</exception>
    public static IReadOnlyList<ShortcodeCall> Parse(string markdown, ISite site, IPage page)
    {
        ArgumentNullException.ThrowIfNull(site);

        return ShortcodeParser.Parse(Tokenize(markdown, site, page),
            name => site.ShortcodeRegistry.Resolve(name, page)?.UsesInner);
    }

    static List<ShortcodeToken> Tokenize(string markdown, ISite site, IPage page) =>
    [
        .. ShortcodeLexer.Tokenize(markdown)
            .Concat(ShortcodeDirectives.Tokenize(markdown,
                name => site.ShortcodeRegistry.Resolve(name, page) is not null))
            .OrderBy(token => token.Start)
    ];

    static bool MayContainShortcodes(string? markdown) =>
        ShortcodeLexer.MayContainShortcodes(markdown) || markdown?.Contains(":::", StringComparison.Ordinal) == true;

    static string RenderCall(ShortcodeCall call, ShortcodeContext? parent, RenderState state)
    {
        if (call.Literal is not null)
        {
            return call.Literal;
        }

        try
        {
            var template = state.Site.ShortcodeRegistry.Resolve(call.Name, state.Page)
                           ?? throw new ShortcodeException($"shortcode '{call.Name}' not found", call.Start);
            var context = ShortcodeBinder.Bind(call.Name, call.Arguments, template, parent, call.Ordinal,
                () => state.PositionText(call.Start));
            if (call.IsPaired)
            {
                context.Inner = Splice(state, call.InnerStart, call.InnerEnd, call.Children,
                    child => RenderCall(child, context, state));
            }

            return RenderTemplate(template, context, state.Site, state.Page);
        }
        catch (Exception ex) when (ex is not ShortcodeException)
        {
            throw new ShortcodeException($"shortcode '{call.Name}': {ex.Message}", call.Start, ex);
        }
    }

    static string Splice(RenderState state, int start, int end, IReadOnlyList<ShortcodeCall> calls,
        Func<ShortcodeCall, string> render)
    {
        var builder = new StringBuilder(end - start);
        var cursor = start;
        foreach (var call in calls)
        {
            builder.Append(state.Source, cursor, call.Start - cursor).Append(render(call));
            cursor = call.End;
        }

        return builder.Append(state.Source, cursor, end - cursor).ToString();
    }

    static string RenderTemplate(ShortcodeTemplate template, ShortcodeContext context, ISite site, IPage page)
    {
        if (template.Builtin is not null)
        {
            return template.Builtin.Render(context, site, page);
        }

        var variables = new Dictionary<string, object>(context.NamedParams, StringComparer.Ordinal)
        {
            ["shortcode"] = context,
            ["params"] = context.Params
        };
        if (context.Inner is not null)
        {
            variables["inner"] = context.Inner;
        }

        return site.TemplateEngine.Render(template.Path ?? template.Body!, site, page, null, variables);
    }

    static string ReplacePlaceholders(string html, List<string> outputs)
    {
        if (outputs.Count == 0)
        {
            return html;
        }

        var builder = new StringBuilder(html.Length);
        var cursor = 0;
        int found;
        while ((found = html.IndexOf(PlaceholderPrefix, cursor, StringComparison.Ordinal)) >= 0)
        {
            if (!TryReadPlaceholder(html, found, outputs.Count, out var index, out var placeholderEnd))
            {
                var skipTo = found + PlaceholderPrefix.Length;
                builder.Append(html, cursor, skipTo - cursor);
                cursor = skipTo;
                continue;
            }

            var (start, end) = WithoutParagraph(html, found, placeholderEnd, cursor);
            builder.Append(html, cursor, start - cursor).Append(outputs[index]);
            cursor = end;
        }

        return builder.Append(html, cursor, html.Length - cursor).ToString();
    }

    static bool TryReadPlaceholder(string html, int found, int count, out int index, out int end)
    {
        var digitsStart = found + PlaceholderPrefix.Length;
        var digitsEnd = digitsStart;
        while (digitsEnd < html.Length && char.IsAsciiDigit(html[digitsEnd]))
        {
            digitsEnd++;
        }

        end = digitsEnd + 1;
        index = -1;
        return digitsEnd > digitsStart
               && digitsEnd < html.Length
               && html[digitsEnd] == PlaceholderSuffix
               && int.TryParse(html.AsSpan(digitsStart, digitsEnd - digitsStart), out index)
               && index < count;
    }

    static (int Start, int End) WithoutParagraph(string html, int start, int end, int cursor)
    {
        const string open = "<p>";
        const string close = "</p>";
        return start - open.Length >= cursor
               && html.AsSpan(start - open.Length, open.Length).SequenceEqual(open)
               && html.AsSpan(end).StartsWith(close, StringComparison.Ordinal)
            ? (start - open.Length, end + close.Length)
            : (start, end);
    }

    /// <summary>
    /// The content being rendered, located in its source file on demand.
    /// </summary>
    sealed class RenderState(string source, ISite site, IPage page)
    {
        int? _lineOffset;

        public string Source { get; } = source;

        public ISite Site { get; } = site;

        public IPage Page { get; } = page;

        string SourcePath => Page.SourceFullPath(Site.SourceContentPath);

        public string PositionText(int offset)
        {
            var (line, column) = LineColumn(offset);
            return $"{SourcePath}:{line}:{column}";
        }

        public ShortcodeException Locate(ShortcodeException exception)
        {
            var (line, column) = LineColumn(exception.Offset);
            var offset = Math.Clamp(exception.Offset, 0, Source.Length);
            var lineStart = offset == 0 ? 0 : Source.LastIndexOf('\n', offset - 1) + 1;
            var lineEnd = Source.IndexOf('\n', lineStart);
            var excerpt = Source[lineStart..(lineEnd < 0 ? Source.Length : lineEnd)].Trim();
            return exception.WithLocation(SourcePath, line, column, excerpt);
        }

        (int Line, int Column) LineColumn(int offset)
        {
            offset = Math.Clamp(offset, 0, Source.Length);
            var line = 1;
            var lineStart = 0;
            for (var index = 0; index < offset; index++)
            {
                if (Source[index] == '\n')
                {
                    line++;
                    lineStart = index + 1;
                }
            }

            _lineOffset ??= FrontMatterLines();
            return (line + _lineOffset.Value, offset - lineStart + 1);
        }

        int FrontMatterLines()
        {
            var path = SourcePath;
            if (!File.Exists(path))
            {
                return 0;
            }

            var text = File.ReadAllText(path);
            return text.EndsWith(Source, StringComparison.Ordinal)
                ? text.AsSpan(0, text.Length - Source.Length).Count('\n')
                : 0;
        }
    }
}
