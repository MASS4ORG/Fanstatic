namespace Fanstatic.Shortcodes;

/// <summary>
/// Reads Docusaurus/remark container directives as Markdown-notation paired shortcode calls:
/// a <c>:::name{key=val} Title</c> line opens, a line of the same number of colons closes, and longer fences nest.
/// Directives whose name is not a shortcode are left to Markdig's custom containers.
/// </summary>
public static class ShortcodeDirectives
{
    /// <summary>
    /// Returns the opening and closing tags of every directive whose name is a shortcode, outside fenced code.
    /// </summary>
    /// <param name="source">The content.</param>
    /// <param name="isShortcode">Tells whether a directive name resolves to a shortcode.</param>
    /// <exception cref="ShortcodeException">A directive attribute list is malformed.</exception>
    public static IReadOnlyList<ShortcodeToken> Tokenize(string source, Func<string, bool> isShortcode)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(isShortcode);

        List<ShortcodeToken> tokens = [];
        if (!source.Contains(":::", StringComparison.Ordinal))
        {
            return tokens;
        }

        Stack<(string Name, int Colons, bool Known)> open = new();
        string? fence = null;
        var lineStart = 0;
        while (lineStart < source.Length)
        {
            var newline = source.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? source.Length : newline;
            var line = source[lineStart..lineEnd].TrimEnd('\r');
            var trimmed = line.TrimStart();
            if (fence is not null)
            {
                fence = IsFenceClose(trimmed, fence) ? null : fence;
            }
            else if (FenceOpening(trimmed) is { } opening)
            {
                fence = opening;
            }
            else
            {
                ReadLine(line, lineStart, newline < 0 ? lineEnd : newline + 1, open, tokens, isShortcode);
            }

            lineStart = lineEnd + 1;
        }

        return tokens;
    }

    static string? FenceOpening(string trimmed)
    {
        var marker = trimmed.Length > 0 && trimmed[0] is '`' or '~' ? trimmed[0] : '\0';
        var count = marker == '\0' ? 0 : CountLeading(trimmed, marker);
        return count >= 3 ? new string(marker, count) : null;
    }

    static bool IsFenceClose(string trimmed, string fence)
    {
        var count = CountLeading(trimmed, fence[0]);
        return count >= fence.Length && string.IsNullOrWhiteSpace(trimmed[count..]);
    }

    static void ReadLine(string line, int lineStart, int nextLineStart, Stack<(string Name, int Colons, bool Known)> open,
        List<ShortcodeToken> tokens, Func<string, bool> isShortcode)
    {
        var indent = line.Length - line.TrimStart(' ').Length;
        var colons = CountLeading(line[indent..], ':');
        if (indent > 3 || colons < 3)
        {
            return;
        }

        var rest = line[(indent + colons)..];
        if (string.IsNullOrWhiteSpace(rest))
        {
            Close(colons, lineStart, lineStart + line.Length, open, tokens);
            return;
        }

        var name = new string([.. rest.TakeWhile(c => !char.IsWhiteSpace(c) && c != '{')]);
        if (name.Length == 0)
        {
            return;
        }

        var known = isShortcode(name);
        open.Push((name, colons, known));
        if (known)
        {
            var arguments = ReadArguments(rest[name.Length..], lineStart + indent + colons + name.Length);
            tokens.Add(new(ShortcodeTokenKind.Open, name, ShortcodeNotation.Markdown, arguments, false, lineStart,
                nextLineStart));
        }
    }

    static void Close(int colons, int start, int end, Stack<(string Name, int Colons, bool Known)> open,
        List<ShortcodeToken> tokens)
    {
        if (open.Count == 0 || open.Peek().Colons != colons)
        {
            return;
        }

        var (name, _, known) = open.Pop();
        if (known)
        {
            tokens.Add(new(ShortcodeTokenKind.Close, name, ShortcodeNotation.Markdown, [], false, start, end));
        }
    }

    static List<ShortcodeArgument> ReadArguments(string text, int offset)
    {
        List<ShortcodeArgument> arguments = [];
        var titleStart = 0;
        if (text.StartsWith('{'))
        {
            titleStart = ReadAttributes(text, offset, arguments);
        }

        var title = text[titleStart..].Trim();
        if (title.Length > 0 && arguments.All(argument => argument.Name != "title"))
        {
            arguments.Add(new("title", title));
        }

        return arguments;
    }

    static int ReadAttributes(string text, int offset, List<ShortcodeArgument> arguments)
    {
        try
        {
            var scanner = new ShortcodeScanner(text, 1);
            while (true)
            {
                scanner.SkipWhitespace();
                if (scanner.AtEnd)
                {
                    throw new ShortcodeException("unclosed directive attributes, expected '}'", 0);
                }

                if (scanner.Current == '}')
                {
                    return scanner.Position + 1;
                }

                var argument = scanner.ReadArgument("}");
                arguments.Add(argument.Name is null ? new(ShortcodeBinder.ToText(argument.Value), true) : argument);
            }
        }
        catch (ShortcodeException ex)
        {
            throw new ShortcodeException(ex.Message, offset + ex.Offset, ex);
        }
    }

    static int CountLeading(string text, char character)
    {
        var count = 0;
        while (count < text.Length && text[count] == character)
        {
            count++;
        }

        return count;
    }
}
