using Fanstatic.Parsers;
using YamlDotNet.Serialization;

namespace Fanstatic.Shortcodes;

/// <summary>
/// Optional parameter schema declared as YAML front matter at the top of a shortcode template.
/// </summary>
[YamlSerializable]
public class ShortcodeSchema
{
    /// <summary>
    /// Names given to positional arguments, in order; declaring them allows mixing positional and named arguments.
    /// </summary>
    public List<string> Positional { get; set; } = [];

    /// <summary>
    /// The accepted parameters by name; when not empty, any other parameter is an error.
    /// </summary>
    public Dictionary<string, ShortcodeParamSchema> Params { get; set; } = [];

    /// <summary>
    /// Splits a template into its schema and its Liquid body; the schema is null when the template has no front matter.
    /// </summary>
    /// <exception cref="FormatException">The front matter is not a valid schema.</exception>
    public static (ShortcodeSchema? Schema, string Body) Split(string template, IFrontMatterParser parser)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(parser);

        var (yaml, body) = SplitFrontMatter(template);
        if (yaml is null)
        {
            return (null, template);
        }

        var schema = string.IsNullOrWhiteSpace(yaml) ? new() : parser.Parse<ShortcodeSchema>(yaml);
        return (schema, body);
    }

    /// <summary>
    /// Returns the Liquid body of a shortcode template, without its front matter.
    /// </summary>
    public static string StripFrontMatter(string template) => SplitFrontMatter(template).Body;

    static (string? Yaml, string Body) SplitFrontMatter(string template)
    {
        if (!template.StartsWith("---\n", StringComparison.Ordinal)
            && !template.StartsWith("---\r\n", StringComparison.Ordinal))
        {
            return (null, template);
        }

        var yamlStart = template.IndexOf('\n') + 1;
        var searchFrom = yamlStart;
        while (searchFrom < template.Length)
        {
            var lineEnd = template.IndexOf('\n', searchFrom);
            var line = (lineEnd < 0 ? template[searchFrom..] : template[searchFrom..lineEnd]).TrimEnd('\r');
            if (line == "---")
            {
                var body = lineEnd < 0 ? string.Empty : template[(lineEnd + 1)..];
                return (template[yamlStart..searchFrom], body);
            }

            if (lineEnd < 0)
            {
                break;
            }

            searchFrom = lineEnd + 1;
        }

        return (null, template);
    }
}

/// <summary>
/// The declared type, presence and allowed values of one shortcode parameter.
/// </summary>
[YamlSerializable]
public class ShortcodeParamSchema
{
    /// <summary>
    /// One of <c>string</c>, <c>int</c>, <c>float</c>, <c>bool</c>, <c>list</c>, <c>map</c> or <c>any</c>.
    /// </summary>
    public string Type { get; set; } = "any";

    /// <summary>
    /// True when a call must pass the parameter.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// The value used when a call omits the parameter, converted to <see cref="Type"/>.
    /// </summary>
    public object? Default { get; set; }

    /// <summary>
    /// The only accepted values, compared as text; empty accepts any value.
    /// </summary>
    public List<string> Allowed { get; set; } = [];
}
