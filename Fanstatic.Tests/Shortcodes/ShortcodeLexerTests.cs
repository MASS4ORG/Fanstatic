using Fanstatic.Shortcodes;
using Xunit;

namespace Fanstatic.Test.Shortcodes;

/// <summary>
/// Tests for the shortcode tag scanner.
/// </summary>
public class ShortcodeLexerTests
{
    /// <summary>Confirms that a standard-notation tag yields its name, notation and offsets.</summary>
    [Fact]
    public void TokenizeShouldReadStandardTag()
    {
        const string source = "a {{< audio >}} b";

        var token = Assert.Single(ShortcodeLexer.Tokenize(source));

        Assert.Equal(ShortcodeTokenKind.Open, token.Kind);
        Assert.Equal("audio", token.Name);
        Assert.Equal(ShortcodeNotation.Standard, token.Notation);
        Assert.Equal(2, token.Start);
        Assert.Equal(source.IndexOf(" b", StringComparison.Ordinal), token.End);
        Assert.False(token.SelfClosing);
    }

    /// <summary>Confirms that the Markdown notation, closing tags and self-closing tags are recognized.</summary>
    [Fact]
    public void TokenizeShouldReadMarkdownClosingAndSelfClosingTags()
    {
        var tokens = ShortcodeLexer.Tokenize("{{% note %}}x{{% /note %}}{{< qr text=\"a\" />}}");

        Assert.Collection(tokens,
            open => Assert.Equal((ShortcodeTokenKind.Open, ShortcodeNotation.Markdown), (open.Kind, open.Notation)),
            close => Assert.Equal((ShortcodeTokenKind.Close, "note"), (close.Kind, close.Name)),
            selfClosing => Assert.True(selfClosing.SelfClosing));
    }

    /// <summary>Confirms that names may contain subdirectories, dashes, dots and underscores.</summary>
    [Fact]
    public void TokenizeShouldAcceptNamesWithSubdirectories()
    {
        var token = Assert.Single(ShortcodeLexer.Tokenize("{{< media/my-audio_v2.x >}}"));

        Assert.Equal("media/my-audio_v2.x", token.Name);
    }

    /// <summary>Confirms that quoted, raw and unquoted values are read, and unquoted scalars are typed.</summary>
    [Fact]
    public void TokenizeShouldTypeArguments()
    {
        var token = Assert.Single(ShortcodeLexer.Tokenize(
            "{{< x a=\"q \\\"x\\\"\" b='s' c=`raw\nline` d=true e=false f=42 g=-3.5 h=9999999999 i=word j=\"7\" >}}"));

        var values = token.Arguments.ToDictionary(argument => argument.Name!, argument => argument.Value);
        Assert.Equal("q \"x\"", values["a"]);
        Assert.Equal("s", values["b"]);
        Assert.Equal("raw\nline", values["c"]);
        Assert.Equal(true, values["d"]);
        Assert.Equal(false, values["e"]);
        Assert.Equal(42, values["f"]);
        Assert.Equal(-3.5, values["g"]);
        Assert.Equal(9999999999L, values["h"]);
        Assert.Equal("word", values["i"]);
        Assert.Equal("7", values["j"]);
    }

    /// <summary>Confirms that positional arguments keep their order and that arguments may span several lines.</summary>
    [Fact]
    public void TokenizeShouldReadMultiLinePositionalArguments()
    {
        var token = Assert.Single(ShortcodeLexer.Tokenize("{{< highlight\n  go\n  \"linenos=table\"\n>}}"));

        Assert.Equal(["go", "linenos=table"], token.Arguments.Select(argument => argument.Value));
        Assert.All(token.Arguments, argument => Assert.Null(argument.Name));
    }

    /// <summary>Confirms that spaces around the equals sign are allowed in named arguments.</summary>
    [Fact]
    public void TokenizeShouldAllowSpacesAroundEquals()
    {
        var argument = Assert.Single(Assert.Single(ShortcodeLexer.Tokenize("{{< x key = \"v\" >}}")).Arguments);

        Assert.Equal(("key", "v"), (argument.Name, argument.Value));
    }

    /// <summary>Confirms that list and map arguments are parsed into collections.</summary>
    [Fact]
    public void TokenizeShouldReadCompositeArguments()
    {
        var token = Assert.Single(ShortcodeLexer.Tokenize(
            "{{< x tags=[\"a\", 'b', 3] size={w: 300, \"h\": 200, nested: {deep: [true]}} empty=[] >}}"));

        var values = token.Arguments.ToDictionary(argument => argument.Name!, argument => argument.Value);
        Assert.Equal(new List<object> { "a", "b", 3 }, values["tags"]);
        var size = Assert.IsType<Dictionary<string, object>>(values["size"]);
        Assert.Equal(300, size["w"]);
        Assert.Equal(200, size["h"]);
        var nested = Assert.IsType<Dictionary<string, object>>(size["nested"]);
        Assert.Equal(new List<object> { true }, nested["deep"]);
        Assert.Empty(Assert.IsType<List<object>>(values["empty"]));
    }

    /// <summary>Confirms that escaped tags produce the literal shortcode without the comment markers.</summary>
    [Theory]
    [InlineData("{{</* audio src=\"x\" */>}}", "{{< audio src=\"x\" >}}")]
    [InlineData("{{%/* note */%}}", "{{% note %}}")]
    [InlineData("{{< /* note */ >}}", "{{< note >}}")]
    [InlineData("{{</*/* note */*/>}}", "{{</* note */>}}")]
    [InlineData("{{</* note /*/>}}", "{{< note />}}")]
    public void TokenizeShouldReadEscapedTags(string source, string literal)
    {
        var token = Assert.Single(ShortcodeLexer.Tokenize(source));

        Assert.Equal(ShortcodeTokenKind.Escaped, token.Kind);
        Assert.Equal(literal, token.Literal);
    }

    /// <summary>Confirms that text without shortcode delimiters yields no tags.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("{{ liquid }} and { {< no")]
    [InlineData("{{{")]
    public void TokenizeShouldIgnoreTextWithoutTags(string source)
    {
        Assert.Empty(ShortcodeLexer.Tokenize(source));
        Assert.False(ShortcodeLexer.MayContainShortcodes(source));
    }

    /// <summary>Confirms that malformed tags fail with the offset of the problem.</summary>
    [Theory]
    [InlineData("ab {{< audio", 3, "unclosed shortcode tag")]
    [InlineData("{{< >}}", 4, "missing shortcode name")]
    [InlineData("{{< au\"dio >}}", 6, "invalid character")]
    [InlineData("{{< x a=\"open >}}", 8, "unterminated string")]
    [InlineData("{{< x a=`open >}}", 8, "unterminated string")]
    [InlineData("{{< x a=[1,", 8, "unterminated list")]
    [InlineData("{{< x a=[1, 2 >}}", 14, "expected ',' or ']'")]
    [InlineData("{{< x a=[1 2] >}}", 11, "expected ',' or ']'")]
    [InlineData("{{< x a={k 1} >}}", 11, "expected ':'")]
    [InlineData("{{< x a={k: 1,", 8, "unterminated map")]
    [InlineData("{{< x a={k: 1 >}}", 14, "expected ',' or '}'")]
    [InlineData("{{< x a= >}}", 9, "expected a value")]
    [InlineData("{{< x a=", 8, "expected a value")]
    [InlineData("{{</* x >}}", 0, "unclosed shortcode comment")]
    [InlineData("{{</* x */ %}}", 0, "unclosed shortcode comment, expected '*/>}}'")]
    [InlineData("{{< /x x >}}", 7, "expected '>}}'")]
    public void TokenizeShouldReportMalformedTags(string source, int offset, string message)
    {
        var exception = Assert.Throws<ShortcodeException>(() => ShortcodeLexer.Tokenize(source));

        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
        Assert.Equal(offset, exception.Offset);
    }

    /// <summary>Confirms that numeric-looking words that are not numbers stay strings.</summary>
    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("-", "-")]
    [InlineData("+x", "+x")]
    [InlineData(".5", 0.5)]
    [InlineData("Infinity", "Infinity")]
    public void ParseScalarShouldOnlyTypeRealNumbers(string word, object expected)
    {
        var token = Assert.Single(ShortcodeLexer.Tokenize($"{{{{< x {word} >}}}}"));

        Assert.Equal(expected, Assert.Single(token.Arguments).Value);
    }
}
