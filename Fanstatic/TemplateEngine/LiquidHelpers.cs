using System.Text;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fluid;
using Fluid.Values;
using Markdig;

namespace Fanstatic.TemplateEngine;

/// <summary>
/// Liquid filters that templates and shortcodes share: <c>markdownify</c>, <c>errorf</c> and <c>warnf</c>.
/// </summary>
public static class LiquidHelpers
{
    /// <summary>
    /// Ambient value key holding the <see cref="ISite"/> being rendered.
    /// </summary>
    public const string SiteAmbientKey = "fanstatic.site";

    /// <summary>
    /// Liquid filter <c>{{ inner | markdownify }}</c>: renders Markdown to HTML, without the paragraph wrapper
    /// when the result is a single paragraph; <c>markdownify: "block"</c> keeps the wrapper.
    /// </summary>
    public static ValueTask<FluidValue> Markdownify(FluidValue input, FilterArguments arguments,
        TemplateContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(context);

        var markdown = input.ToStringValue();
        var html = (context.AmbientValues.TryGetValue(SiteAmbientKey, out var value) && value is ISite site
                    && context.GetValue("page").ToObjectValue() is IPage page
            ? WikiLinkContext.Run(site, page, () => Markdown.ToHtml(markdown, SiteHelper.MarkdownPipeline))
            : Markdown.ToHtml(markdown, SiteHelper.MarkdownPipeline)).Trim();
        const string open = "<p>";
        const string close = "</p>";
        if (arguments.At(0).ToStringValue() != "block"
            && html.StartsWith(open, StringComparison.Ordinal) && html.EndsWith(close, StringComparison.Ordinal)
            && html.IndexOf(open, open.Length, StringComparison.Ordinal) < 0)
        {
            html = html[open.Length..^close.Length];
        }

        return new(FluidValue.Create(html, context.Options));
    }

    /// <summary>
    /// Liquid filter <c>{{ "src is required: %s" | errorf: name }}</c>: fails the render, and so the build,
    /// with the formatted message.
    /// </summary>
    /// <exception cref="InvalidOperationException">Always, carrying the formatted message.</exception>
    public static ValueTask<FluidValue> Errorf(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        ArgumentNullException.ThrowIfNull(input);

        throw new InvalidOperationException(Format(input.ToStringValue(), arguments));
    }

    /// <summary>
    /// Liquid filter <c>{{ "deprecated: %s" | warnf: name }}</c>: logs the formatted message as a warning and
    /// outputs nothing.
    /// </summary>
    public static ValueTask<FluidValue> Warnf(FluidValue input, FilterArguments arguments, TemplateContext context)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        if (context.AmbientValues.TryGetValue(SiteAmbientKey, out var value) && value is ISite site)
        {
            var file = context.GetValue("page").ToObjectValue() is IPage page ? page.SourceRelativePath : null;
            site.Logger.Warning("{Message} (rendering {File})", Format(input.ToStringValue(), arguments), file);
        }

        return new(StringValue.Empty);
    }

    /// <summary>
    /// Replaces the <c>%s</c>, <c>%v</c>, <c>%d</c> and <c>%q</c> verbs with the arguments in order; <c>%%</c> is a
    /// literal percent sign.
    /// </summary>
    public static string Format(string format, FilterArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(arguments);

        var builder = new StringBuilder(format.Length);
        var next = 0;
        for (var index = 0; index < format.Length; index++)
        {
            if (format[index] != '%' || index + 1 >= format.Length)
            {
                builder.Append(format[index]);
                continue;
            }

            AppendVerb(builder, format[++index], arguments, ref next);
        }

        return builder.ToString();
    }

    static void AppendVerb(StringBuilder builder, char verb, FilterArguments arguments, ref int next)
    {
        if (verb == '%' || "svdq".IndexOf(verb, StringComparison.Ordinal) < 0)
        {
            builder.Append(verb == '%' ? "%" : "%" + verb);
            return;
        }

        var argument = next < arguments.Count ? arguments.At(next++).ToStringValue() : "%!" + verb + "(MISSING)";
        builder.Append(verb == 'q' ? $"\"{argument}\"" : argument);
    }
}
