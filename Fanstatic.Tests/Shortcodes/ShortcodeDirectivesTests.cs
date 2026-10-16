using Fanstatic.Shortcodes;
using Xunit;

namespace Fanstatic.Test.Shortcodes;

/// <summary>
/// Tests for reading <c>:::</c> container directives as shortcode calls.
/// </summary>
public class ShortcodeDirectivesTests
{
    static IReadOnlyList<ShortcodeToken> Tokenize(string source) =>
        ShortcodeDirectives.Tokenize(source, name => name is "note" or "box");

    /// <summary>Confirms that a directive becomes a Markdown-notation paired call with attributes and a title.</summary>
    [Fact]
    public void TokenizeShouldReadAttributesAndTitle()
    {
        const string source = "intro\n:::note{kind=warn size=\"big\" .wide} The *title*\nbody\n:::\nafter";

        var tokens = Tokenize(source);

        Assert.Equal(2, tokens.Count);
        var open = tokens[0];
        Assert.Equal((ShortcodeTokenKind.Open, "note", ShortcodeNotation.Markdown),
            (open.Kind, open.Name, open.Notation));
        Assert.Equal(source.IndexOf(":::note", StringComparison.Ordinal), open.Start);
        Assert.Equal(source.IndexOf("body", StringComparison.Ordinal), open.End);
        Assert.Equal(
            [("kind", "warn"), ("size", "big"), (".wide", true), ("title", "The *title*")],
            open.Arguments.Select(argument => (argument.Name, argument.Value)));
        var close = tokens[1];
        Assert.Equal(ShortcodeTokenKind.Close, close.Kind);
        Assert.Equal("\nafter", source[close.End..]);
    }

    /// <summary>Confirms that longer fences nest and unknown directives are skipped while keeping the nesting.</summary>
    [Fact]
    public void TokenizeShouldNestLongerFencesAndSkipUnknownNames()
    {
        const string source = "::::note\n:::tip\nx\n:::\n:::box\ny\n:::\n::::\n";

        var tokens = Tokenize(source);

        Assert.Equal(
            [(ShortcodeTokenKind.Open, "note"), (ShortcodeTokenKind.Open, "box"), (ShortcodeTokenKind.Close, "box"),
                (ShortcodeTokenKind.Close, "note")],
            tokens.Select(token => (token.Kind, token.Name)));
    }

    /// <summary>Confirms that directives inside fenced code, indented lines and lone closing fences are ignored.</summary>
    [Fact]
    public void TokenizeShouldIgnoreCodeFencesAndStrayLines()
    {
        const string source = "```\n:::note\n:::\n```\n~~~~\n:::note\n~~~~\n    :::note\n:::\n::: \n:: note\n:::{x}\n";

        Assert.Empty(Tokenize(source));
        Assert.Empty(Tokenize("no directives"));
    }

    /// <summary>Confirms that a title attribute wins over the trailing title text and a missing close yields no closing tag.</summary>
    [Fact]
    public void TokenizeShouldPreferTitleAttribute()
    {
        var open = Assert.Single(Tokenize(":::note{title=\"Attr\"} Text"));

        Assert.Equal(("title", "Attr"), (open.Arguments.Single().Name, open.Arguments.Single().Value));
        Assert.Equal(":::note{title=\"Attr\"} Text".Length, open.End);
    }

    /// <summary>Confirms that malformed attribute lists fail at their offset in the content.</summary>
    [Theory]
    [InlineData("x\n:::note{a=\"open}\n:::\n", 12, "unterminated string")]
    [InlineData(":::note{a=1\n:::\n", 7, "unclosed directive attributes")]
    public void TokenizeShouldReportMalformedAttributes(string source, int offset, string message)
    {
        var exception = Assert.Throws<ShortcodeException>(() => Tokenize(source));

        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
        Assert.Equal(offset, exception.Offset);
    }
}
