using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using Fanstatic.Models;
using Fanstatic.TemplateEngine;
using Fluid;
using Fluid.Ast;
using Fluid.Utils;
using Fluid.Values;
using Parlot.Fluent;

namespace Fanstatic.Shortcodes;

/// <summary>
/// Fluid parser with the <c>shortcode</c> tag, so layouts call shortcodes as content does:
/// <c>{% shortcode 'audio', src: page.Params.audio %}</c>, or <c>{% shortcode 'note' %}…{% endshortcode %}</c>
/// for shortcodes that use <c>inner</c>. Argument values are Liquid expressions.
/// </summary>
public sealed class ShortcodeFluidParser : FluidParser
{
    readonly ConditionalWeakTable<ParseContext, Stack<HeaderState>> _headers = new();

    /// <summary>
    /// Creates the parser.
    /// </summary>
    /// <param name="options">Fluid parser options.</param>
    /// <param name="usesInner">Tells whether a shortcode takes inner content, which makes the tag a block.</param>
    public ShortcodeFluidParser(FluidParserOptions options, Func<string, bool> usesInner) : base(options)
    {
        ArgumentNullException.ThrowIfNull(usesInner);

        var header = Primary
            .And(ZeroOrOne(Terms.Char(',').SkipAnd(ArgumentsList)))
            .AndSkip(TagEnd)
            .Then<HeaderState>((_, tag) => new(
                tag.Item1,
                tag.Item2 is { Count: > 0 } ? tag.Item2[0] : null));
        var body = AnyTagsList
            .AndSkip(CreateTag("endshortcode").ElseError("'{% endshortcode %}' was expected"));
        var standalone = Always<Statement?>(null)
            .Then<Statement>((context, _) => CreateStatement(context, null));
        var block = body
            .Then<Statement>((context, statements) => CreateStatement(context, statements));

        RegisteredTags["shortcode"] = header.Switch(
            (context, state) =>
            {
                _headers.GetValue(context, _ => new Stack<HeaderState>()).Push(state);
                return state.Name is LiteralExpression { Value: StringValue name }
                       && usesInner(name.ToStringValue(null))
                    ? 1
                    : 0;
            },
            standalone,
            block);
    }

    /// <summary>
    /// Pops the shortcode header parsed immediately before the selected tag body.
    /// </summary>
    ShortcodeStatement CreateStatement(ParseContext context, IReadOnlyList<Statement>? body)
    {
        var state = _headers.GetValue(context, _ => new Stack<HeaderState>()).Pop();
        return new(state.Name, state.Arguments, body);
    }

    /// <summary>
    /// A <c>shortcode</c> tag: evaluates the name and arguments, renders the block as <c>inner</c>, and writes the
    /// shortcode output unencoded.
    /// </summary>
    sealed class ShortcodeStatement(
        Expression name,
        IReadOnlyList<FilterArgument>? arguments,
        IReadOnlyList<Statement>? body) : Statement
    {
        public override async ValueTask<Completion> WriteToAsync(IFluidOutput output, TextEncoder encoder,
            TemplateContext context)
        {
            var shortcodeName = (await name.EvaluateAsync(context)).ToStringValue(context);
            var values = await EvaluateArgumentsAsync(context);
            var inner = body is null ? null : await RenderBodyAsync(body, encoder, context);
            var (site, page) = Caller(context);

            output.Write(ShortcodeRenderer.RenderFromTemplate(shortcodeName, values, inner, site, page));
            return Completion.Normal;
        }

        async ValueTask<List<ShortcodeArgument>> EvaluateArgumentsAsync(TemplateContext context)
        {
            List<ShortcodeArgument> values = [];
            foreach (var argument in arguments ?? [])
            {
                var value = await argument.Expression.EvaluateAsync(context);
                if (!value.IsNil())
                {
                    values.Add(new(string.IsNullOrEmpty(argument.Name) ? null : argument.Name,
                        ToClr(value, context)));
                }
            }

            return values;
        }

        static async ValueTask<string> RenderBodyAsync(IReadOnlyList<Statement> statements, TextEncoder encoder,
            TemplateContext context)
        {
            using var writer = new StringWriter();
            await using var output = new TextWriterFluidOutput(writer, 256, leaveOpen: true);

            foreach (var statement in statements)
            {
                _ = await statement.WriteToAsync(output, encoder, context);
            }

            await output.FlushAsync();
            return writer.ToString();
        }

        static (ISite Site, IPage Page) Caller(TemplateContext context) =>
            context.AmbientValues.GetValueOrDefault(LiquidHelpers.SiteAmbientKey) is ISite site
            && context.GetValue("page").ToObjectValue(context) is IPage page
                ? (site, page)
                : throw new InvalidOperationException("the shortcode tag needs a page to render");

        static object ToClr(FluidValue value, TemplateContext context) => value switch
        {
            BooleanValue => value.ToBooleanValue(context),
            NumberValue => NumberToClr(value.ToNumberValue(context), value.IsInteger(context)),
            ArrayValue array => array.Values.Select(item => ToClr(item, context)).ToList(),
            StringValue => value.ToStringValue(context),
            _ => value.ToObjectValue(context) ?? value.ToStringValue(context)
        };

        static object NumberToClr(decimal number, bool isInteger)
        {
            if (!isInteger)
            {
                return (double)number;
            }

            return number is >= int.MinValue and <= int.MaxValue ? (int)number : (object)(long)number;
        }
    }

    sealed record HeaderState(Expression Name, IReadOnlyList<FilterArgument>? Arguments);
}
