using System.Globalization;

namespace Fanstatic.Shortcodes;

/// <summary>
/// Turns call arguments into a <see cref="ShortcodeContext"/>, enforcing Hugo's argument rules and the template schema.
/// </summary>
public static class ShortcodeBinder
{
    /// <summary>
    /// Binds the arguments of one call.
    /// </summary>
    /// <param name="name">The shortcode name.</param>
    /// <param name="arguments">The call arguments in source order.</param>
    /// <param name="template">The resolved template, which may declare positional names and a schema.</param>
    /// <param name="parent">The enclosing shortcode, if any.</param>
    /// <param name="ordinal">Position among the calls of the same parent.</param>
    /// <param name="position">Computes the call location as <c>file:line:col</c>.</param>
    /// <exception cref="ArgumentException">The arguments break the call rules or the schema.</exception>
    public static ShortcodeContext Bind(string name, IReadOnlyList<ShortcodeArgument> arguments,
        ShortcodeTemplate template, ShortcodeContext? parent, int ordinal, Func<string>? position)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(template);

        Dictionary<string, object> named = new(StringComparer.Ordinal);
        List<object> positional = [];
        foreach (var argument in arguments)
        {
            if (argument.Name is null)
            {
                positional.Add(argument.Value);
            }
            else
            {
                named[argument.Name] = argument.Value;
            }
        }

        var positionalNames = template.PositionalNames;
        if (positionalNames.Count == 0 && positional.Count > 0 && named.Count > 0)
        {
            throw new ArgumentException("cannot mix named and positional arguments");
        }

        MapPositional(positional, named, positionalNames);
        if (template.Schema is { } schema)
        {
            Validate(schema, named, positional);
        }

        return new()
        {
            Name = name,
            NamedParams = named,
            PositionalParams = positional,
            Parent = parent,
            Ordinal = ordinal,
            PositionFactory = position
        };
    }

    static void MapPositional(List<object> positional, Dictionary<string, object> named,
        IReadOnlyList<string> positionalNames)
    {
        if (positionalNames.Count == 0)
        {
            return;
        }

        if (positional.Count > positionalNames.Count)
        {
            throw new ArgumentException(
                $"too many positional arguments: expected at most {positionalNames.Count} ({string.Join(", ", positionalNames)})");
        }

        for (var index = 0; index < positional.Count; index++)
        {
            if (!named.TryAdd(positionalNames[index], positional[index]))
            {
                throw new ArgumentException($"argument '{positionalNames[index]}' is passed both by position and by name");
            }
        }

        positional.Clear();
    }

    static void Validate(ShortcodeSchema schema, Dictionary<string, object> named, List<object> positional)
    {
        if (schema.Params.Count == 0)
        {
            return;
        }

        if (positional.Count > 0)
        {
            throw new ArgumentException("takes named arguments; declare 'Positional' in its schema to accept positional ones");
        }

        var unknown = named.Keys.FirstOrDefault(key => !schema.Params.ContainsKey(key));
        if (unknown is not null)
        {
            throw new ArgumentException(
                $"unknown parameter '{unknown}'; expected one of: {string.Join(", ", schema.Params.Keys)}");
        }

        foreach (var (paramName, spec) in schema.Params)
        {
            if (named.TryGetValue(paramName, out var value))
            {
                named[paramName] = CheckAllowed(paramName, spec, Coerce(paramName, spec.Type, value));
            }
            else if (spec.Required)
            {
                throw new ArgumentException($"missing required parameter '{paramName}'");
            }
            else if (spec.Default is not null)
            {
                named[paramName] = Coerce(paramName, spec.Type, spec.Default);
            }
        }
    }

    static object CheckAllowed(string paramName, ShortcodeParamSchema spec, object value)
    {
        var text = ToText(value);
        return spec.Allowed.Count == 0 || spec.Allowed.Contains(text, StringComparer.Ordinal)
            ? value
            : throw new ArgumentException(
                $"parameter '{paramName}' must be one of: {string.Join(", ", spec.Allowed)}; got '{text}'");
    }

    /// <summary>
    /// Converts a value to a schema type, accepting text that parses as the type.
    /// </summary>
    /// <exception cref="ArgumentException">The value cannot be converted.</exception>
    public static object Coerce(string paramName, string? type, object value)
    {
        if (!Converters.TryGetValue(type ?? string.Empty, out var convert))
        {
            throw new ArgumentException($"parameter '{paramName}' declares unknown type '{type}'");
        }

        return convert(value) ?? throw new ArgumentException(
            $"parameter '{paramName}' must be {type}; got '{ToText(value)}'");
    }

    static readonly Dictionary<string, Func<object, object?>> Converters = new(StringComparer.OrdinalIgnoreCase)
    {
        [string.Empty] = value => value,
        ["any"] = value => value,
        ["string"] = value => value is IList<object> or IDictionary<string, object> ? null : ToText(value),
        ["int"] = ToInt,
        ["float"] = ToFloat,
        ["bool"] = ToBool,
        ["list"] = value => value as List<object>,
        ["map"] = value => value as Dictionary<string, object>
    };

    static object? ToInt(object value) => value switch
    {
        int or long => value,
        double number when number == Math.Floor(number) => (long)number,
        string text => ParseInt(text),
        _ => null
    };

    static object? ParseInt(string text)
    {
        if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed))
        {
            return null;
        }

        return parsed is >= int.MinValue and <= int.MaxValue ? (int)parsed : (object)parsed;
    }

    static object? ToBool(object value) =>
        value as bool? ?? (value is string text && bool.TryParse(text, out var flag) ? flag : null);

    static object? ToFloat(object value) => value switch
    {
        double => value,
        int integer => (double)integer,
        long integer => (double)integer,
        string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture,
            out var parsed) => parsed,
        _ => null
    };

    /// <summary>
    /// Formats a scalar with the invariant culture.
    /// </summary>
    public static string ToText(object? value) => value switch
    {
        null => string.Empty,
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}
