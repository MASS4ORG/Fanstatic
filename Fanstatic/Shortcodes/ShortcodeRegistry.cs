using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Fanstatic.Helpers;
using Fanstatic.Models;

namespace Fanstatic.Shortcodes;

/// <summary>
/// Finds shortcode templates: <c>&lt;site&gt;/shortcodes/</c>, then <c>&lt;theme&gt;/shortcodes/</c>, then the
/// built-ins. Within a folder, the most specific file wins: <c>name.&lt;lang&gt;.&lt;format&gt;.&lt;ext&gt;</c>,
/// <c>name.&lt;format&gt;.&lt;ext&gt;</c>, <c>name.&lt;lang&gt;.&lt;ext&gt;</c>, <c>name.&lt;ext&gt;</c>, and HTML
/// templates for other output formats.
/// </summary>
public sealed partial class ShortcodeRegistry
{
    /// <summary>
    /// Folder name of shortcode templates in the site and theme.
    /// </summary>
    public const string FolderName = "shortcodes";

    const string EmbeddedPrefix = "Fanstatic.wwwroot.shortcodes.";

    readonly ISite _site;

    readonly Dictionary<string, IBuiltinShortcode> _builtins;

    readonly ConcurrentDictionary<(string Name, string Language, string Format), ShortcodeTemplate?> _cache = new();

    /// <summary>
    /// Creates a registry for a site with the given C# built-ins.
    /// </summary>
    public ShortcodeRegistry(ISite site, IEnumerable<IBuiltinShortcode> builtins)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(builtins);

        _site = site;
        _builtins = builtins.ToDictionary(builtin => builtin.Name, StringComparer.Ordinal);
    }

    [GeneratedRegex(@"\b(inner|Inner|InnerDeindent)\b")]
    private static partial Regex InnerUsageRegex();

    /// <summary>
    /// Forgets resolved templates so changed files are read again.
    /// </summary>
    public void Clear() => _cache.Clear();

    /// <summary>
    /// Resolves a shortcode for a page's language and output format, or null when no template exists.
    /// </summary>
    /// <exception cref="FormatException">The template schema is invalid.</exception>
    public ShortcodeTemplate? Resolve(string name, IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return Resolve(name, page.Language.Code, page.OutputFormat);
    }

    /// <summary>
    /// Resolves a shortcode for a language and output format, or null when no template exists.
    /// </summary>
    /// <exception cref="FormatException">The template schema is invalid.</exception>
    public ShortcodeTemplate? Resolve(string name, string? language, string outputFormat)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(outputFormat);

        if (!IsSafeName(name))
        {
            return null;
        }

        var key = (name, language ?? string.Empty, outputFormat);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        return _cache.GetOrAdd(key, Load(name, language, outputFormat));
    }

    /// <summary>
    /// Folders searched for template files, most specific first.
    /// </summary>
    public IEnumerable<string> Roots =>
    [
        Path.Combine(_site.Options.Source, FolderName),
        Path.Combine(_site.SourceThemePath, FolderName)
    ];

    ShortcodeTemplate? Load(string name, string? language, string outputFormat)
    {
        var candidates = CandidateFileNames(name, language, outputFormat).ToList();
        foreach (var root in Roots)
        {
            var file = candidates.Select(candidate => Path.Combine(root, candidate)).FirstOrDefault(File.Exists);
            if (file is not null)
            {
                return FromLiquid(name, Path.GetFullPath(file), File.ReadAllText(file));
            }
        }

        if (_builtins.TryGetValue(name, out var builtin))
        {
            return new(name, null, null, builtin, builtin.UsesInner, null);
        }

        return candidates.Select(LoadEmbedded).FirstOrDefault(body => body is not null) is { } embedded
            ? FromLiquid(name, null, embedded)
            : null;
    }

    ShortcodeTemplate FromLiquid(string name, string? path, string text)
    {
        var (schema, body) = ShortcodeSchema.Split(text, _site.Parser);
        return new(name, path, path is null ? body : null, null, InnerUsageRegex().IsMatch(body), schema);
    }

    /// <summary>
    /// File names tried in each folder, most specific first.
    /// </summary>
    public static IEnumerable<string> CandidateFileNames(string name, string? language, string outputFormat)
    {
        var extension = FileUtils.OutputFormats.TryGetValue(outputFormat, out var format)
            ? format.Extension
            : outputFormat;
        var hasLanguage = !string.IsNullOrEmpty(language);

        List<string> names = [];
        if (hasLanguage)
        {
            names.Add($"{name}.{language}.{outputFormat}.{extension}");
        }

        names.Add($"{name}.{outputFormat}.{extension}");
        if (hasLanguage)
        {
            names.Add($"{name}.{language}.{extension}");
        }

        names.Add($"{name}.{extension}");
        if (!extension.Equals("html", StringComparison.OrdinalIgnoreCase))
        {
            if (hasLanguage)
            {
                names.Add($"{name}.{language}.html");
            }

            names.Add($"{name}.html");
        }

        return names.Distinct(StringComparer.Ordinal);
    }

    static string? LoadEmbedded(string fileName)
    {
        using var stream = typeof(ShortcodeRegistry).Assembly
            .GetManifestResourceStream(EmbeddedPrefix + fileName.Replace('/', '.'));
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    static bool IsSafeName(string name) =>
        !name.StartsWith('/') && name.Split('/').All(segment => segment.Length > 0 && segment != "." && segment != "..");
}
