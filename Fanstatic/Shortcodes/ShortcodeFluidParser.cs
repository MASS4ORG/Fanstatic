using System.Text.Encodings.Web;
using Fanstatic.Models;
using Fanstatic.TemplateEngine;
using Fluid;
using Fluid.Ast;
using Fluid.Values;

namespace Fanstatic.Shortcodes;

/// <summary>
/// Fluid parser with the <c>shortcode</c> tag, so layouts call shortcodes as content does:
/// <c>{% shortcode 'audio', src: page.Params.audio %}</c>, or <c>{% shortcode 'note' %}…{% endshortcode %}</c>
/// for shortcodes that use <c>inner</c>. Argument values are Liquid expressions.
/// </summary>
public sealed class ShortcodeFluidParser : FluidParser
{
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
            .AndSkip(TagEnd);
        var body = AnyTagsList
            .AndSkip(CreateTag("endshortcode").ElseError("'{% endshortcode %}' was expected"));

        RegisteredTags["shortcode"] = header.Switch((_, tag) =>
            tag.Item1 is LiteralExpression { Value: StringValue name } && usesInner(name.ToStringValue())
                ? body.Then<Statement>(statements => new ShortcodeStatement(tag.Item1, tag.Item2, statements))
                : Always<Statement>(new ShortcodeStatement(tag.Item1, tag.Item2, null)));
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
        public override async ValueTask<Completion> WriteToAsync(TextWriter writer, TextEncoder encoder,
            TemplateContext context)
        {
            var shortcodeName = (await name.EvaluateAsync(context)).ToStringValue();
            var values = await EvaluateArgumentsAsync(context);
            var inner = body is null ? null : await RenderBodyAsync(body, encoder, context);
            var (site, page) = Caller(context);

            await writer.WriteAsync(ShortcodeRenderer.RenderFromTemplate(shortcodeName, values, inner, site, page));
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
                    values.Add(new(string.IsNullOrEmpty(argument.Name) ? null : argument.Name, ToClr(value)));
                }
            }

            return values;
        }

        static async ValueTask<string> RenderBodyAsync(IReadOnlyList<Statement> statements, TextEncoder encoder,
            TemplateContext context)
        {
            await using var innerWriter = new StringWriter();
            foreach (var statement in statements)
            {
                _ = await statement.WriteToAsync(innerWriter, encoder, context);
            }

            return innerWriter.ToString();
        }

        static (ISite Site, IPage Page) Caller(TemplateContext context) =>
            context.AmbientValues.GetValueOrDefault(LiquidHelpers.SiteAmbientKey) is ISite site
            && context.GetValue("page").ToObjectValue() is IPage page
                ? (site, page)
                : throw new InvalidOperationException("the shortcode tag needs a page to render");

        static object ToClr(FluidValue value) => value switch
        {
            BooleanValue => value.ToBooleanValue(),
            NumberValue => NumberToClr(value.ToNumberValue(), value.IsInteger()),
            ArrayValue array => array.Values.Select(ToClr).ToList(),
            StringValue => value.ToStringValue(),
            _ => value.ToObjectValue() ?? value.ToStringValue()
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
}
