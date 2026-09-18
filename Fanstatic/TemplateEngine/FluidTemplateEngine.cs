using System.Collections.Concurrent;
using Fluid;
using Fluid.Values;
using Fanstatic.Models;

namespace Fanstatic.TemplateEngine;

/// <summary>
/// Fluid template engine.
/// </summary>
public class FluidTemplateEngine : ITemplateEngine
{
    /// <summary>
    /// The Fluid parser instance.
    /// </summary>
    FluidParser FluidParser { get; } = new(new()
    {
        AllowLiquidTag = true,
    });

    /// <summary>
    /// The Fluid/Liquid template options.
    /// </summary>
    TemplateOptions TemplateOptions { get; } = new();

    /// <summary>
    /// Cache of compiled templates by template content.
    /// </summary>
    readonly ConcurrentDictionary<string, IFluidTemplate?> _compiledCache = new();

    /// <summary>
    /// Cache of template body by template key/path.
    /// </summary>
    readonly ConcurrentDictionary<string, string> _templateBodyByKey =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Current initialized theme path.
    /// </summary>
    string? _themePath;

    /// <summary>
    /// Site reference set by Initialize(); used by the paginate filter.
    /// </summary>
    Site? _site;

    /// <summary>
    /// ctor
    /// </summary>
    public FluidTemplateEngine()
    {
        TemplateOptions.MemberAccessStrategy.Register<FanstaticInfo>();
        TemplateOptions.MemberAccessStrategy.Register<SiteOutput>();
        TemplateOptions.MemberAccessStrategy.Register<LanguageSettings>();
        TemplateOptions.MemberAccessStrategy.Register<Page>();
        TemplateOptions.MemberAccessStrategy.Register<IOutput>();
        TemplateOptions.MemberAccessStrategy.Register<Resource>();
        TemplateOptions.MemberAccessStrategy.Register<Theme>();
        TemplateOptions.MemberAccessStrategy.Register<Pager>();

        TemplateOptions.Filters.AddFilter("whereParams", WhereParamsFilter);
        TemplateOptions.Filters.AddFilter("paginate", PaginateFilter);
        TemplateOptions.Filters.AddFilter("i18n", I18NFilter);
    }

    /// <inheritdoc/>
    public void Initialize(Site site)
    {
        ArgumentNullException.ThrowIfNull(site);

        _site = site;
        _themePath = Path.GetFullPath(site.SourceThemePath);

        _compiledCache.Clear();
        _templateBodyByKey.Clear();

        TemplateOptions.FileProvider = new LiquidPhysicalFileProvider(_themePath);
    }

    /// <inheritdoc/>
    public void PreCompileTheme(string themePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themePath);

        var fullThemePath = Path.GetFullPath(themePath);
        if (!Directory.Exists(fullThemePath))
        {
            return;
        }

        _themePath = fullThemePath;
        _compiledCache.Clear();
        _templateBodyByKey.Clear();

        foreach (var file in Directory.EnumerateFiles(fullThemePath, "*.*", SearchOption.AllDirectories))
        {
            if (!IsTemplateFile(file))
            {
                continue;
            }

            var body = File.ReadAllText(file);

            var relativePath = UrlifyPath(Path.GetRelativePath(fullThemePath, file));
            var fullPath = UrlifyPath(file);

            _templateBodyByKey[relativePath] = body;
            _templateBodyByKey[fullPath] = body;
            _templateBodyByKey['/' + relativePath] = body;

            _ = _compiledCache.GetOrAdd(body, d => FluidParser.TryParse(d, out var t, out _) ? t : null);
        }
    }

    /// <inheritdoc/>
    public string Render(string templatePathOrInlineKey, ISite site, IPage page, int? counter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templatePathOrInlineKey);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(page);

        var templateBody = ResolveTemplateBody(templatePathOrInlineKey);
        var template = GetCompiledTemplate(templateBody);

        var context = SeedContext(site, page, counter);
        return template.Render(context);
    }

    /// <inheritdoc/>
    public string RenderInline(string templateBody, ISite site, IPage page)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateBody);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(page);

        var template = GetCompiledTemplate(templateBody);

        var context = SeedContext(site, page);
        return template.Render(context);
    }

    TemplateContext SeedContext(ISite site, IPage page, int? counter = null)
    {
        var context = new TemplateContext(TemplateOptions)
            .SetValue("fanstatic", site.Fanstatic)
            .SetValue("site", page.Site)
            .SetValue("page", page);

        if (counter.HasValue)
        {
            _ = context.SetValue("counter", counter.Value);
        }

        return context;
    }

    IFluidTemplate GetCompiledTemplate(string templateBody)
    {
        var template = _compiledCache.GetOrAdd(templateBody,
            d => FluidParser.TryParse(d, out var t, out _) ? t : null);

        if (template is not null)
        {
            return template;
        }

        _ = FluidParser.TryParse(templateBody, out _, out var error);
        throw new FormatException(error);
    }

    string ResolveTemplateBody(string templatePathOrInlineKey)
    {
        if (_templateBodyByKey.TryGetValue(templatePathOrInlineKey, out var cached))
        {
            return cached;
        }

        var normalizedKey = UrlifyPath(templatePathOrInlineKey);
        if (_templateBodyByKey.TryGetValue(normalizedKey, out cached))
        {
            return cached;
        }

        if (File.Exists(templatePathOrInlineKey))
        {
            var body = File.ReadAllText(templatePathOrInlineKey);
            _templateBodyByKey[UrlifyPath(templatePathOrInlineKey)] = body;
            return body;
        }

        if (!string.IsNullOrEmpty(_themePath))
        {
            var combined = Path.Combine(_themePath, normalizedKey.TrimStart('/'));
            if (File.Exists(combined))
            {
                var body = File.ReadAllText(combined);

                var fullKey = UrlifyPath(combined);
                var relKey = UrlifyPath(Path.GetRelativePath(_themePath, combined));

                _templateBodyByKey[fullKey] = body;
                _templateBodyByKey[relKey] = body;
                _templateBodyByKey['/' + relKey] = body;

                return body;
            }
        }

        return templatePathOrInlineKey;
    }

    static bool IsTemplateFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".liquid", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".html", StringComparison.OrdinalIgnoreCase);
    }

    static string UrlifyPath(string path) => path.Replace('\\', '/');

    /// <summary>
    /// Liquid filter: <c>pages_list | paginate: N</c>
    /// Returns a <see cref="Pager"/> for the current page index and registers virtual
    /// pages 2..count into OutputReferences (only when PageIndex == 1).
    /// Also sets <c>page.Paginator</c> so the partial can use either the returned value
    /// or <c>page.Paginator</c>.
    /// </summary>
    ValueTask<FluidValue> PaginateFilter(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        if (_site is null) return new ValueTask<FluidValue>(NilValue.Instance);

        // Build items list from filter input
        var items = new List<IPage>();
        if (input is ArrayValue arr)
        {
            foreach (var v in arr.Values)
            {
                if (v.ToObjectValue() is IPage p) items.Add(p);
            }
        }

        if (items.Count == 0) return new ValueTask<FluidValue>(NilValue.Instance);

        var pageSize = arguments.Count > 0
            ? (int)arguments.At(0).ToNumberValue()
            : _site.Paginate;
        if (pageSize <= 0) return new ValueTask<FluidValue>(NilValue.Instance);

        if (context.GetValue("page").ToObjectValue() is not Page currentPage)
            return new ValueTask<FluidValue>(NilValue.Instance);

        var total = items.Count;
        var count = (int)Math.Ceiling(total / (double)pageSize);
        var current = currentPage.PageIndex;

        var pageNums = Enumerable.Range(1, count).ToList();
        var pageItems = (IReadOnlyList<IPage>)[.. items
            .Skip((current - 1) * pageSize)
            .Take(pageSize)];

        // BaseUrl: strip /{paginatePath}/{index} suffix for virtual pages
        var relDir = (currentPage as IOutput).RelPermalinkDir.ToString().TrimEnd('/');
        if (current > 1)
        {
            var suffix = $"/{_site.PaginatePath}/{current}";
            if (relDir.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                relDir = relDir[..^suffix.Length];
        }

        var baseUrl = relDir + "/";

        var pager = new Pager(
            count, current, 1, count,
            current > 1 ? current - 1 : null,
            current < count ? current + 1 : null,
            pageNums, pageItems, baseUrl, _site.PaginatePath);

        currentPage.SetPaginator(pager);

        // Register virtual pages only once, from the first page
        if (current == 1 && count > 1)
            _site.RegisterVirtualPagesFromFilter(currentPage, count);

        return new ValueTask<FluidValue>(FluidValue.Create(pager, context.Options));
    }

    /// <summary>
    /// Liquid filter: <c>{{ "key" | i18n }}</c> or <c>{{ "key" | i18n: count }}</c>
    /// Translates a key into the current page's language, falling back to the default
    /// language. Supports Hugo-style pluralization: when a count argument is provided
    /// and the entry has <c>one</c>/<c>other</c> fields, the singular form is used
    /// for <c>count == 1</c>.
    /// </summary>
    ValueTask<FluidValue> I18NFilter(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        if (_site is null) return new ValueTask<FluidValue>(NilValue.Instance);

        var key = input.ToStringValue();
        if (string.IsNullOrEmpty(key)) return new ValueTask<FluidValue>(NilValue.Instance);

        if (context.GetValue("page").ToObjectValue() is not IPage page) return new ValueTask<FluidValue>(NilValue.Instance);

        var translations = _site.I18N;
        if (translations.Count == 0) return new ValueTask<FluidValue>(NilValue.Instance);

        // Try current language, fall back to default
        var lang = page.Language.Code;
        if (!translations.TryGetValue(lang, out var langEntries))
        {
            lang = _site.DefaultLanguage;
            if (!translations.TryGetValue(lang, out langEntries))
                return new ValueTask<FluidValue>(NilValue.Instance);
        }

        if (!langEntries.TryGetValue(key, out var entry))
            return new ValueTask<FluidValue>(NilValue.Instance);

        string? result;
        var count = arguments.Count > 0 ? (int)arguments.At(0).ToNumberValue() : 0;
        if (count == 1 && !string.IsNullOrEmpty(entry.One))
        {
            result = entry.One;
        }
        else
        {
            result = entry.Other;
        }

        if (string.IsNullOrEmpty(result))
            return new ValueTask<FluidValue>(NilValue.Instance);

        // Render as inline template to support embedded Liquid expressions
        if (result.Contains('{'))
        {
            var rendered = _site.TemplateEngine.RenderInline(result, _site, page);
            return new ValueTask<FluidValue>(FluidValue.Create(rendered, context.Options));
        }

        return new ValueTask<FluidValue>(FluidValue.Create(result, context.Options));
    }

    /// <summary>
    /// Fluid/Liquid filter to navigate Params dictionary.
    /// </summary>
    static ValueTask<FluidValue> WhereParamsFilter(FluidValue input, FilterArguments arguments,
        TemplateContext _)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(arguments);

        List<FluidValue> result = [];
        var list = (input as ArrayValue)!.Values;

        var keys = arguments.At(0).ToStringValue().Split('.');
        foreach (var item in list)
        {
            if (item.ToObjectValue() is IParams param &&
                CheckValueInDictionary(keys, param.Params, arguments.At(1).ToStringValue()))
            {
                result.Add(item);
            }
        }

        return new ArrayValue(result);
    }

    static bool CheckValueInDictionary(string[] array, IReadOnlyDictionary<string, object> dictionary,
        string value)
    {
        var currentDictionary = dictionary;
        for (var i = 0; i < array.Length; i++)
        {
            var key = array[i];

            if (!currentDictionary.TryGetValue(key, out var dictionaryValue))
            {
                return false;
            }

            if (i == array.Length - 1)
            {
                return dictionaryValue.Equals(value);
            }

            if (dictionaryValue is not Dictionary<string, object> nestedDictionary)
            {
                return false;
            }

            currentDictionary = nestedDictionary;
        }

        return false;
    }
}
