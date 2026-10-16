namespace Fanstatic.Shortcodes;

/// <summary>
/// Splits content into shortcode tags: <c>{{&lt; name args &gt;}}</c>, <c>{{% name args %}}</c>, their closing
/// and self-closing forms, and the escaped <c>{{&lt;/* name */&gt;}}</c> form.
/// </summary>
public static class ShortcodeLexer
{
    /// <summary>
    /// Returns every shortcode tag in <paramref name="source"/>, in order.
    /// </summary>
    /// <exception cref="ShortcodeException">A tag is malformed or never closed.</exception>
    public static IReadOnlyList<ShortcodeToken> Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        List<ShortcodeToken> tokens = [];
        var index = FindOpening(source, 0);
        while (index >= 0)
        {
            var token = ReadTag(source, index);
            tokens.Add(token);
            index = FindOpening(source, token.End);
        }

        return tokens;
    }

    /// <summary>
    /// True when <paramref name="source"/> may hold a shortcode tag.
    /// </summary>
    public static bool MayContainShortcodes(string? source) =>
        !string.IsNullOrEmpty(source) && FindOpening(source, 0) >= 0;

    static int FindOpening(string source, int from)
    {
        while ((from = source.IndexOf("{{", from, StringComparison.Ordinal)) >= 0)
        {
            if (from + 2 < source.Length && source[from + 2] is '<' or '%')
            {
                return from;
            }

            from++;
        }

        return -1;
    }

    static ShortcodeToken ReadTag(string source, int start)
    {
        var notation = source[start + 2] == '<' ? ShortcodeNotation.Standard : ShortcodeNotation.Markdown;
        var close = notation == ShortcodeNotation.Standard ? ">}}" : "%}}";
        var scanner = new ShortcodeScanner(source, start + 3);
        scanner.SkipWhitespace();

        if (scanner.StartsWith("/*"))
        {
            return ReadEscaped(source, start, scanner.Position, notation, close);
        }

        return !scanner.AtEnd && scanner.Current == '/'
            ? ReadClosing(ref scanner, start, notation, close)
            : ReadOpening(ref scanner, start, notation, close);
    }

    static ShortcodeToken ReadOpening(ref ShortcodeScanner scanner, int start, ShortcodeNotation notation,
        string close)
    {
        var name = scanner.ReadName(close);
        List<ShortcodeArgument> arguments = [];
        while (true)
        {
            scanner.SkipWhitespace();
            if (scanner.AtEnd)
            {
                throw new ShortcodeException($"unclosed shortcode tag '{name}', expected '{close}'", start);
            }

            if (scanner.AtTagEnd(close))
            {
                var selfClosing = scanner.Current == '/';
                scanner.Position += close.Length + (selfClosing ? 1 : 0);
                return new(ShortcodeTokenKind.Open, name, notation, arguments, selfClosing, start, scanner.Position);
            }

            arguments.Add(scanner.ReadArgument(close));
        }
    }

    static ShortcodeToken ReadClosing(ref ShortcodeScanner scanner, int start, ShortcodeNotation notation,
        string close)
    {
        scanner.Position++;
        scanner.SkipWhitespace();
        var name = scanner.ReadName(close);
        scanner.SkipWhitespace();
        scanner.Expect(close);
        return new(ShortcodeTokenKind.Close, name, notation, [], false, start, scanner.Position);
    }

    static ShortcodeToken ReadEscaped(string source, int start, int commentStart, ShortcodeNotation notation,
        string close)
    {
        var commentEnd = commentStart + 1;
        while ((commentEnd = source.IndexOf("*/", commentEnd + 1, StringComparison.Ordinal)) >= 0)
        {
            var scanner = new ShortcodeScanner(source, commentEnd + 2);
            scanner.SkipWhitespace();
            if (scanner.StartsWith(close))
            {
                var open = notation == ShortcodeNotation.Standard ? "{{<" : "{{%";
                var literal = open + source[(commentStart + 2)..commentEnd] + close;
                return new(ShortcodeTokenKind.Escaped, string.Empty, notation, [], false, start,
                    scanner.Position + close.Length, literal);
            }
        }

        throw new ShortcodeException($"unclosed shortcode comment, expected '*/{close}'", start);
    }
}
