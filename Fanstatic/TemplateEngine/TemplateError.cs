namespace Fanstatic.TemplateEngine;

/// <summary>
/// A template failure captured while validating the theme or while rendering a page.
/// </summary>
/// <param name="TemplatePath">Theme template path the failure belongs to, or <c>(inline template)</c>.</param>
/// <param name="Message">Failure description without the position suffix.</param>
/// <param name="Line">1-based line reported by the template engine, 0 when unknown.</param>
/// <param name="Column">1-based column reported by the template engine, 0 when unknown.</param>
/// <param name="SourceExcerpt">Offending source line, when the engine reports one.</param>
/// <param name="PageSourceRelativePath">Content file being rendered, null for theme validation failures.</param>
public sealed record TemplateError(
    string TemplatePath,
    string Message,
    int Line,
    int Column,
    string? SourceExcerpt,
    string? PageSourceRelativePath)
{
    const string SourceMarker = "\nSource:\n";

    const string PositionPrefix = "at (";

    /// <summary>
    /// Quoted failure location, with the position when the engine reports one.
    /// </summary>
    public string Location =>
        Line > 0 ? $"'{TemplatePath}:{Line}:{Column}'" : $"'{TemplatePath}'";

    /// <summary>
    /// Builds an error from Fluid's parse output, splitting off the position and the source excerpt.
    /// </summary>
    /// <param name="templatePath">Template the parse failure belongs to.</param>
    /// <param name="parseError">Fluid parse error text.</param>
    /// <param name="templateBody">Template body, used to derive an excerpt when Fluid reports none.</param>
    public static TemplateError FromFluidParseError(string templatePath, string? parseError, string? templateBody)
    {
        var message = string.IsNullOrWhiteSpace(parseError) ? "Template could not be parsed." : parseError.Trim();
        string? excerpt = null;

        var sourceMarker = message.IndexOf(SourceMarker, StringComparison.Ordinal);
        if (sourceMarker >= 0)
        {
            excerpt = message[(sourceMarker + SourceMarker.Length)..].Trim();
            message = message[..sourceMarker].Trim();
        }

        var line = 0;
        var column = 0;

        var positionStart = message.LastIndexOf(PositionPrefix, StringComparison.Ordinal);
        if (positionStart >= 0)
        {
            var positionEnd = message.IndexOf(')', positionStart);
            if (positionEnd > positionStart)
            {
                var parts = message[(positionStart + PositionPrefix.Length)..positionEnd].Split(':');
                if (parts.Length == 2
                    && int.TryParse(parts[0], out var parsedLine)
                    && int.TryParse(parts[1], out var parsedColumn))
                {
                    line = parsedLine;
                    column = parsedColumn;
                    message = message[..positionStart].Trim();
                }
            }
        }

        return new TemplateError(templatePath, message, line, column,
            NullIfEmpty(excerpt) ?? SourceLineAt(templateBody, line), null);
    }

    /// <summary>
    /// Builds an error from an exception raised while rendering a template.
    /// </summary>
    /// <param name="templatePath">Template being rendered.</param>
    /// <param name="pageSourceRelativePath">Content file being rendered.</param>
    /// <param name="exception">Exception raised by the template engine.</param>
    public static TemplateError FromRenderException(string templatePath, string? pageSourceRelativePath,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            TemplateRenderException render => new TemplateError(templatePath, render.Message, render.Line, render.Column,
                render.SourceExcerpt, pageSourceRelativePath),
            FormatException => FromFluidParseError(templatePath, exception.Message, null)
                with
            { PageSourceRelativePath = pageSourceRelativePath },
            _ => new TemplateError(templatePath, exception.Message, 0, 0, null, pageSourceRelativePath)
        };
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        var page = PageSourceRelativePath is null ? string.Empty : $" (rendering '{PageSourceRelativePath}')";
        var excerpt = SourceExcerpt is null ? string.Empty : $"\n  {SourceExcerpt}";

        return $"{Location}: {Message}{page}{excerpt}";
    }

    static string? SourceLineAt(string? templateBody, int line)
    {
        if (string.IsNullOrEmpty(templateBody) || line <= 0)
        {
            return null;
        }

        var lines = templateBody.Split('\n');
        return line <= lines.Length ? lines[line - 1].Trim() : null;
    }

    static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
