using Fanstatic.Models;

namespace Fanstatic.Shortcodes;

/// <summary>
/// The <c>param</c> built-in: a page parameter by dotted key, falling back to the site parameter.
/// Usage: <c>{{&lt; param "author.name" &gt;}}</c>; a missing key fails the build.
/// </summary>
public sealed class ParamShortcode : IBuiltinShortcode
{
    /// <inheritdoc/>
    public string Name => "param";

    /// <inheritdoc/>
    public bool UsesInner => false;

    /// <inheritdoc/>
    public IReadOnlyList<string> Positional => ["name"];

    /// <inheritdoc/>
    public string Render(ShortcodeContext shortcode, ISite site, IPage page)
    {
        ArgumentNullException.ThrowIfNull(shortcode);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(page);

        var key = shortcode.GetString(0, "name");
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("requires a parameter name");
        }

        var value = Find(page.Params, key) ?? Find(site.Params, key)
            ?? throw new ArgumentException($"param '{key}' not found in the page or site params");
        return value is not string and IEnumerable<object> items
            ? string.Join(", ", items.Select(ShortcodeBinder.ToText))
            : ShortcodeBinder.ToText(value);
    }

    /// <summary>
    /// Follows a dotted key through nested maps, matching keys case-insensitively as Hugo does.
    /// </summary>
    public static object? Find(IEnumerable<KeyValuePair<string, object>>? values, string key)
    {
        object? current = values;
        foreach (var segment in key.Split('.'))
        {
            current = current is IEnumerable<KeyValuePair<string, object>> map ? Lookup(map, segment) : null;
        }

        return current;
    }

    static object? Lookup(IEnumerable<KeyValuePair<string, object>> map, string segment) =>
        map.FirstOrDefault(pair => pair.Key.Equals(segment, StringComparison.OrdinalIgnoreCase)).Value;
}
