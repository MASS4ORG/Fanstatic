using System.Globalization;
using System.Text;

namespace Fanstatic.Shortcodes;

/// <summary>
/// Cursor over shortcode source text that reads names, arguments and typed values.
/// </summary>
ref struct ShortcodeScanner
{
    readonly ReadOnlySpan<char> _source;

    /// <summary>
    /// Creates a scanner positioned at <paramref name="position"/>.
    /// </summary>
    public ShortcodeScanner(ReadOnlySpan<char> source, int position)
    {
        _source = source;
        Position = position;
    }

    /// <summary>
    /// Current offset in the source.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// True when the cursor is past the last character.
    /// </summary>
    public readonly bool AtEnd => Position >= _source.Length;

    /// <summary>
    /// The character under the cursor.
    /// </summary>
    public readonly char Current => _source[Position];

    /// <summary>
    /// True when the remaining source starts with <paramref name="text"/>.
    /// </summary>
    public readonly bool StartsWith(string text) =>
        _source[Position..].StartsWith(text, StringComparison.Ordinal);

    /// <summary>
    /// Advances past spaces, tabs and line breaks.
    /// </summary>
    public void SkipWhitespace()
    {
        while (!AtEnd && char.IsWhiteSpace(Current))
        {
            Position++;
        }
    }

    /// <summary>
    /// Consumes <paramref name="text"/> or fails at the current position.
    /// </summary>
    public void Expect(string text)
    {
        if (!StartsWith(text))
        {
            throw new ShortcodeException($"expected '{text}'", Position);
        }

        Position += text.Length;
    }

    /// <summary>
    /// Reads a shortcode name such as <c>note</c> or <c>media/audio</c>.
    /// </summary>
    public string ReadName(string close)
    {
        var start = Position;
        while (!AtEnd && !char.IsWhiteSpace(Current) && !AtTagEnd(close))
        {
            if (!IsNameChar(Current))
            {
                throw new ShortcodeException($"invalid character '{Current}' in shortcode name", Position);
            }

            Position++;
        }

        return Position > start
            ? _source[start..Position].ToString()
            : throw new ShortcodeException("missing shortcode name", start);
    }

    /// <summary>
    /// True when the cursor is on the closing delimiter, self-closing or not.
    /// </summary>
    public readonly bool AtTagEnd(string close) => StartsWith(close) || StartsWith("/" + close);

    /// <summary>
    /// Reads one positional (<c>value</c>) or named (<c>key=value</c>) argument.
    /// </summary>
    public ShortcodeArgument ReadArgument(string close)
    {
        if (StartsDelimitedValue(Current))
        {
            return new(null, ReadValue(close, composite: false));
        }

        var word = ReadBareWord(close, composite: false);
        var afterWord = Position;
        SkipWhitespace();
        if (AtEnd || Current != '=')
        {
            Position = afterWord;
            return new(null, ParseScalar(word));
        }

        Position++;
        SkipWhitespace();
        return new(word, ReadValue(close, composite: false));
    }

    /// <summary>
    /// Reads a quoted string, a raw string, a list, a map or a typed bare scalar.
    /// </summary>
    public object ReadValue(string close, bool composite)
    {
        if (AtEnd)
        {
            throw new ShortcodeException("expected a value", Position);
        }

        return Current switch
        {
            '"' => ReadDoubleQuoted(),
            '\'' or '`' => ReadDelimited(Current),
            '[' => ReadList(close),
            '{' => ReadMap(close),
            _ => ParseScalar(ReadBareWord(close, composite))
        };
    }

    static bool StartsDelimitedValue(char character) => character is '"' or '\'' or '`' or '[' or '{';

    readonly bool AtEscape => Current == '\\' && Position + 1 < _source.Length && _source[Position + 1] is '"' or '\\';

    string ReadDoubleQuoted()
    {
        var start = Position++;
        var builder = new StringBuilder();
        while (!AtEnd && Current != '"')
        {
            if (AtEscape)
            {
                Position++;
            }

            builder.Append(Current);
            Position++;
        }

        if (AtEnd)
        {
            throw new ShortcodeException("unterminated string", start);
        }

        Position++;
        return builder.ToString();
    }

    string ReadDelimited(char delimiter)
    {
        var start = Position++;
        var end = _source[Position..].IndexOf(delimiter);
        if (end < 0)
        {
            throw new ShortcodeException("unterminated string", start);
        }

        var value = _source.Slice(Position, end).ToString();
        Position += end + 1;
        return value;
    }

    List<object> ReadList(string close)
    {
        var start = Position++;
        List<object> items = [];
        while (!ReadCompositeEnd(']', start))
        {
            items.Add(ReadValue(close, composite: true));
            ReadCompositeSeparator(']');
        }

        return items;
    }

    Dictionary<string, object> ReadMap(string close)
    {
        var start = Position++;
        Dictionary<string, object> map = new(StringComparer.Ordinal);
        while (!ReadCompositeEnd('}', start))
        {
            var key = ReadMapKey(close);
            map[key] = ReadValue(close, composite: true);
            ReadCompositeSeparator('}');
        }

        return map;
    }

    string ReadMapKey(string close)
    {
        var key = Current is '"' or '\'' or '`'
            ? ShortcodeBinder.ToText(ReadValue(close, composite: true))
            : ReadBareWord(close, composite: true);
        ExpectMapSeparator(key);
        return key;
    }

    void ExpectMapSeparator(string key)
    {
        SkipWhitespace();
        if (AtEnd || Current is not (':' or '='))
        {
            throw new ShortcodeException($"expected ':' after map key '{key}'", Position);
        }

        Position++;
        SkipWhitespace();
    }

    bool ReadCompositeEnd(char end, int start)
    {
        SkipWhitespace();
        if (AtEnd)
        {
            throw new ShortcodeException(end == ']' ? "unterminated list" : "unterminated map", start);
        }

        if (Current != end)
        {
            return false;
        }

        Position++;
        return true;
    }

    void ReadCompositeSeparator(char end)
    {
        SkipWhitespace();
        if (!AtEnd && Current == ',')
        {
            Position++;
        }
        else if (AtEnd || Current != end)
        {
            throw new ShortcodeException($"expected ',' or '{end}'", Position);
        }
    }

    string ReadBareWord(string close, bool composite)
    {
        var start = Position;
        while (!AtEnd && !char.IsWhiteSpace(Current) && !AtTagEnd(close) && !IsWordStop(Current, composite))
        {
            Position++;
        }

        return Position > start
            ? _source[start..Position].ToString()
            : throw new ShortcodeException("expected a value", start);
    }

    static bool IsWordStop(char c, bool composite) =>
        composite ? c is ',' or ']' or '}' or ':' : c == '=';

    static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or '/';

    /// <summary>
    /// Types an unquoted value as Hugo does: <c>true</c>/<c>false</c>, integers and floats; anything else stays a string.
    /// </summary>
    public static object ParseScalar(string word)
    {
        if (word is "true" or "false")
        {
            return word == "true";
        }

        if (!LooksNumeric(word))
        {
            return word;
        }

        if (int.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
        {
            return integer;
        }

        if (long.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var longInteger))
        {
            return longInteger;
        }

        return double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : word;
    }

    static bool LooksNumeric(string word)
    {
        var first = word[0];
        return char.IsAsciiDigit(first)
               || (first is '-' or '+' or '.' && word.Length > 1 && (char.IsAsciiDigit(word[1]) || word[1] == '.'));
    }
}
