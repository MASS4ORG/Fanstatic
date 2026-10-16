using Fanstatic.Parsers;
using Fanstatic.Shortcodes;
using Xunit;

namespace Fanstatic.Test.Shortcodes;

/// <summary>
/// Tests for shortcode parameter schemas and argument binding.
/// </summary>
public class ShortcodeSchemaTests
{
    const string CardTemplate = """
                                ---
                                Positional: [title, width]
                                Params:
                                  title: { Type: string, Required: true }
                                  width: { Type: int, Default: 300 }
                                  ratio: { Type: float }
                                  kind: { Type: string, Allowed: [note, warn], Default: note }
                                  open: { Type: bool }
                                  tags: { Type: list }
                                  size: { Type: map }
                                  extra: {}
                                ---
                                <div>{{ title }}</div>
                                """;

    static readonly YamlParser Parser = new();

    static ShortcodeTemplate Card()
    {
        var (schema, body) = ShortcodeSchema.Split(CardTemplate.Replace("\r\n", "\n"), Parser);
        return new("card", null, body, null, false, schema);
    }

    static ShortcodeContext Bind(ShortcodeTemplate template, params ShortcodeArgument[] arguments) =>
        ShortcodeBinder.Bind(template.Name, arguments, template, null, 0, null);

    /// <summary>Confirms that the front matter is parsed into a schema and stripped from the body.</summary>
    [Fact]
    public void SplitShouldParseSchemaAndStripFrontMatter()
    {
        var template = Card();

        Assert.Equal("<div>{{ title }}</div>", template.Body);
        Assert.Equal(["title", "width"], template.Schema!.Positional);
        Assert.True(template.Schema.Params["title"].Required);
        Assert.Equal(["note", "warn"], template.Schema.Params["kind"].Allowed);
        Assert.Equal("any", template.Schema.Params["extra"].Type);
    }

    /// <summary>Confirms that templates without a closed front matter keep their whole body and have no schema.</summary>
    [Theory]
    [InlineData("<div></div>")]
    [InlineData("---\nnot closed")]
    [InlineData("--- not front matter\n---\n")]
    public void SplitShouldKeepTemplatesWithoutFrontMatter(string template)
    {
        var (schema, body) = ShortcodeSchema.Split(template, Parser);

        Assert.Null(schema);
        Assert.Equal(template, body);
        Assert.Equal(template, ShortcodeSchema.StripFrontMatter(template));
    }

    /// <summary>Confirms that an empty front matter yields an empty schema and CRLF files are supported.</summary>
    [Fact]
    public void SplitShouldAcceptEmptyAndCrlfFrontMatter()
    {
        var (empty, emptyBody) = ShortcodeSchema.Split("---\n---\nbody", Parser);
        Assert.Empty(empty!.Params);
        Assert.Equal("body", emptyBody);

        var (crlf, crlfBody) = ShortcodeSchema.Split("---\r\nPositional: [a]\r\n---\r\nbody", Parser);
        Assert.Equal(["a"], crlf!.Positional);
        Assert.Equal("body", crlfBody);
        Assert.Equal(string.Empty, ShortcodeSchema.StripFrontMatter("---\n---"));
    }

    /// <summary>Confirms that an invalid schema fails with a format error.</summary>
    [Fact]
    public void SplitShouldRejectInvalidSchema() =>
        Assert.Throws<FormatException>(() => ShortcodeSchema.Split("---\nParams: [1, 2]\n---\nx", Parser));

    /// <summary>Confirms that positional arguments map to the declared names, mixed with named ones, and defaults apply.</summary>
    [Fact]
    public void BindShouldMapPositionalArgumentsAndApplyDefaults()
    {
        var context = Bind(Card(), new(null, "Hello"), new("kind", "warn"));

        Assert.True(context.IsNamedParams);
        Assert.Equal("Hello", context.NamedParams["title"]);
        Assert.Equal("warn", context.NamedParams["kind"]);
        Assert.Equal(300, context.NamedParams["width"]);
        Assert.False(context.NamedParams.ContainsKey("ratio"));
        Assert.Same(context.NamedParams, context.Params);
    }

    /// <summary>Confirms that values are converted to the declared types, including text that parses as the type.</summary>
    [Fact]
    public void BindShouldCoerceDeclaredTypes()
    {
        var context = Bind(Card(), new("title", 42), new("width", "120"), new("ratio", 2), new("open", "true"),
            new("tags", new List<object> { "a" }), new("size", new Dictionary<string, object> { ["w"] = 1 }),
            new("extra", 1.5));

        Assert.Equal("42", context.NamedParams["title"]);
        Assert.Equal(120, context.NamedParams["width"]);
        Assert.Equal(2.0, context.NamedParams["ratio"]);
        Assert.Equal(true, context.NamedParams["open"]);
        Assert.Equal(1.5, context.NamedParams["extra"]);
    }

    /// <summary>Confirms that schema violations are reported with clear messages.</summary>
    [Theory]
    [InlineData("missing required parameter 'title'")]
    [InlineData("unknown parameter 'nope'; expected one of: title, width, ratio, kind, open, tags, size, extra", "nope", "x")]
    [InlineData("parameter 'width' must be int; got 'wide'", "width", "wide")]
    [InlineData("parameter 'ratio' must be float; got 'x'", "ratio", "x")]
    [InlineData("parameter 'open' must be bool; got 'maybe'", "open", "maybe")]
    [InlineData("parameter 'tags' must be list; got 'a'", "tags", "a")]
    [InlineData("parameter 'size' must be map; got 'a'", "size", "a")]
    [InlineData("parameter 'kind' must be one of: note, warn; got 'tip'", "kind", "tip")]
    public void BindShouldReportSchemaViolations(string message, string? name = null, string? value = null)
    {
        List<ShortcodeArgument> arguments = name is "title" ? [] : [new("title", "t")];
        if (name is not null)
        {
            arguments.Add(new(name, value!));
        }

        if (message.StartsWith("missing", StringComparison.Ordinal))
        {
            arguments.Clear();
        }

        var exception = Assert.Throws<ArgumentException>(() => Bind(Card(), [.. arguments]));

        Assert.Equal(message, exception.Message);
    }

    /// <summary>Confirms the positional argument rules: arity, duplicates and positional calls to named-only schemas.</summary>
    [Fact]
    public void BindShouldEnforcePositionalRules()
    {
        Assert.Equal("too many positional arguments: expected at most 2 (title, width)",
            Assert.Throws<ArgumentException>(() => Bind(Card(), new(null, "a"), new(null, 1), new(null, 2))).Message);
        Assert.Equal("argument 'title' is passed both by position and by name",
            Assert.Throws<ArgumentException>(() => Bind(Card(), new(null, "a"), new("title", "b"))).Message);

        var named = new ShortcodeTemplate("n", null, "", null, false, new ShortcodeSchema
        {
            Params = { ["a"] = new ShortcodeParamSchema() }
        });
        Assert.StartsWith("takes named arguments",
            Assert.Throws<ArgumentException>(() => Bind(named, new ShortcodeArgument(null, "x"))).Message,
            StringComparison.Ordinal);
    }

    /// <summary>Confirms that without a schema positional arguments stay a list and named ones a map.</summary>
    [Fact]
    public void BindWithoutSchemaShouldKeepArgumentShapes()
    {
        var plain = new ShortcodeTemplate("p", null, "", null, false, null);

        var positional = Bind(plain, new(null, "a"), new(null, 2));
        Assert.False(positional.IsNamedParams);
        Assert.Equal(new List<object> { "a", 2 }, positional.Params);
        Assert.Equal(2, positional.Get(1));
        Assert.Null(positional.Get(5));
        Assert.Null(positional.Get("a"));
        Assert.Null(positional.Get(1.5));
        Assert.Equal("a", positional.GetString(0, "x"));

        var empty = Bind(plain);
        Assert.Empty(Assert.IsType<List<object>>(empty.Params));
        Assert.Equal(string.Empty, empty.Position);
        Assert.Null(empty.InnerDeindent);
    }

    /// <summary>Confirms that an unknown declared type is reported.</summary>
    [Fact]
    public void CoerceShouldRejectUnknownTypes()
    {
        Assert.Equal("parameter 'x' declares unknown type 'date'",
            Assert.Throws<ArgumentException>(() => ShortcodeBinder.Coerce("x", "date", "v")).Message);
        Assert.Equal(5L, ShortcodeBinder.Coerce("x", "int", 5.0));
        Assert.Equal(9999999999L, ShortcodeBinder.Coerce("x", "int", "9999999999"));
        Assert.Equal(3.0, ShortcodeBinder.Coerce("x", "float", 3L));
        Assert.Equal("v", ShortcodeBinder.Coerce("x", null, "v"));
        Assert.Equal(string.Empty, ShortcodeBinder.ToText(null));
        Assert.Equal("false", ShortcodeBinder.ToText(false));
    }
}
