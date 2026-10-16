using Fanstatic.Shortcodes;
using Xunit;

namespace Fanstatic.Test.Shortcodes;

/// <summary>
/// Tests for building the shortcode call tree.
/// </summary>
public class ShortcodeParserTests
{
    static readonly Dictionary<string, bool> Templates = new()
    {
        ["note"] = true,
        ["box"] = true,
        ["audio"] = false,
        ["video"] = false
    };

    static IReadOnlyList<ShortcodeCall> Parse(string source) =>
        ShortcodeParser.Parse(ShortcodeLexer.Tokenize(source),
            name => Templates.TryGetValue(name, out var usesInner) ? usesInner : null);

    /// <summary>Confirms that paired calls nest their children with parent links and per-parent ordinals.</summary>
    [Fact]
    public void ParseShouldNestCallsWithParentAndOrdinal()
    {
        const string source = "{{< audio >}}{{< note >}}a{{< video >}}b{{< box >}}{{< video >}}{{< /box >}}{{< /note >}}";

        var roots = Parse(source);

        Assert.Equal(2, roots.Count);
        var note = roots[1];
        Assert.Equal(("note", 1, true), (note.Name, note.Ordinal, note.IsPaired));
        Assert.Equal(source.Length, note.End);
        Assert.Equal("a{{< video >}}b{{< box >}}{{< video >}}{{< /box >}}",
            source[note.InnerStart..note.InnerEnd]);
        Assert.Collection(note.Children,
            video => Assert.Equal(("video", 0, note), (video.Name, video.Ordinal, video.Parent)),
            box => Assert.Equal(("box", 1, note), (box.Name, box.Ordinal, box.Parent)));
        var inner = Assert.Single(note.Children[1].Children);
        Assert.Same(note.Children[1], inner.Parent);
        Assert.Equal(0, inner.Ordinal);
    }

    /// <summary>Confirms that a call to a template without inner stays standalone and a self-closed paired call has no inner.</summary>
    [Fact]
    public void ParseShouldKeepStandaloneAndSelfClosedCallsUnpaired()
    {
        var roots = Parse("{{< audio >}} {{< note />}}");

        Assert.All(roots, call => Assert.False(call.IsPaired));
        Assert.Empty(roots[1].Children);
    }

    /// <summary>Confirms that escaped tags become literal entries that do not count as ordinals.</summary>
    [Fact]
    public void ParseShouldKeepEscapedTagsAsLiterals()
    {
        var roots = Parse("{{</* note */>}}{{< audio >}}");

        Assert.Equal("{{< note >}}", roots[0].Literal);
        Assert.Equal(0, roots[1].Ordinal);
    }

    /// <summary>Confirms that unknown, unclosed, mismatched and unexpected closing tags fail at the offending tag.</summary>
    [Theory]
    [InlineData("ab {{< missing >}}", 3, "shortcode 'missing' not found")]
    [InlineData("{{< note >}}x", 0, "unclosed shortcode 'note'")]
    [InlineData("x{{< audio >}}{{< /audio >}}", 14, "does not use inner")]
    [InlineData("{{< /note >}}", 0, "has no opening tag")]
    [InlineData("{{< note >}}{{< box >}}{{< /note >}}", 23, "found while 'box' is open")]
    [InlineData("{{< note >}}{{< /missing >}}", 12, "shortcode 'missing' not found")]
    public void ParseShouldReportUnbalancedCalls(string source, int offset, string message)
    {
        var exception = Assert.Throws<ShortcodeException>(() => Parse(source));

        Assert.Contains(message, exception.Message, StringComparison.Ordinal);
        Assert.Equal(offset, exception.Offset);
    }

    /// <summary>Confirms that an invalid template schema is reported at the call that resolves it.</summary>
    [Fact]
    public void ParseShouldReportTemplateFormatErrorsAtTheCall()
    {
        var tokens = ShortcodeLexer.Tokenize("ab {{< bad >}}");

        var exception = Assert.Throws<ShortcodeException>(() =>
            ShortcodeParser.Parse(tokens, _ => throw new FormatException("bad schema")));

        Assert.Equal("shortcode 'bad': bad schema", exception.Message);
        Assert.Equal(3, exception.Offset);
    }
}
