namespace Fanstatic.Shortcodes;

/// <summary>
/// Builds the nested call tree from lexed tags. As in Hugo, a call is paired when its template uses
/// <c>inner</c>: such a call needs a closing tag or the self-closing form, and other calls cannot have one.
/// </summary>
public static class ShortcodeParser
{
    /// <summary>
    /// Returns the top-level calls of <paramref name="tokens"/>, nesting the calls found between paired tags.
    /// </summary>
    /// <param name="tokens">Tags from <see cref="ShortcodeLexer.Tokenize"/>.</param>
    /// <param name="usesInner">Tells whether a shortcode uses <c>inner</c>; null when the shortcode is unknown.</param>
    /// <exception cref="ShortcodeException">A shortcode is unknown, unclosed or closed by the wrong tag.</exception>
    public static IReadOnlyList<ShortcodeCall> Parse(IReadOnlyList<ShortcodeToken> tokens,
        Func<string, bool?> usesInner)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(usesInner);

        List<ShortcodeCall> roots = [];
        Stack<ShortcodeCall> open = new();
        foreach (var token in tokens)
        {
            var siblings = open.Count > 0 ? open.Peek().Children : roots;
            switch (token.Kind)
            {
                case ShortcodeTokenKind.Escaped:
                    siblings.Add(new()
                    {
                        Name = string.Empty,
                        Literal = token.Literal,
                        Start = token.Start,
                        End = token.End
                    });
                    break;
                case ShortcodeTokenKind.Open:
                    AddOpening(token, siblings, open, usesInner);
                    break;
                default:
                    Close(token, open, usesInner);
                    break;
            }
        }

        return open.Count == 0
            ? roots
            : throw new ShortcodeException(
                $"unclosed shortcode '{open.Peek().Name}': add a closing tag or use the self-closing form",
                open.Peek().Start);
    }

    static void AddOpening(ShortcodeToken token, List<ShortcodeCall> siblings, Stack<ShortcodeCall> open,
        Func<string, bool?> usesInner)
    {
        var paired = !token.SelfClosing && Resolve(token, usesInner);
        var call = new ShortcodeCall
        {
            Name = token.Name,
            Notation = token.Notation,
            Arguments = token.Arguments,
            Parent = open.Count > 0 ? open.Peek() : null,
            Ordinal = siblings.Count(sibling => sibling.Literal is null),
            Start = token.Start,
            End = token.End,
            InnerStart = paired ? token.End : -1
        };
        siblings.Add(call);

        if (paired)
        {
            open.Push(call);
        }
    }

    static void Close(ShortcodeToken token, Stack<ShortcodeCall> open, Func<string, bool?> usesInner)
    {
        if (open.Count > 0 && open.Peek().Name == token.Name)
        {
            var call = open.Pop();
            call.InnerEnd = token.Start;
            call.End = token.End;
            return;
        }

        if (!Resolve(token, usesInner))
        {
            throw new ShortcodeException(
                $"shortcode '{token.Name}' does not use inner, so it cannot have a closing tag", token.Start);
        }

        throw new ShortcodeException(open.Count == 0
            ? $"closing tag for shortcode '{token.Name}' has no opening tag"
            : $"closing tag for shortcode '{token.Name}' found while '{open.Peek().Name}' is open", token.Start);
    }

    static bool Resolve(ShortcodeToken token, Func<string, bool?> usesInner)
    {
        bool? result;
        try
        {
            result = usesInner(token.Name);
        }
        catch (FormatException ex)
        {
            throw new ShortcodeException($"shortcode '{token.Name}': {ex.Message}", token.Start, ex);
        }

        return result ?? throw new ShortcodeException($"shortcode '{token.Name}' not found", token.Start);
    }
}
