namespace Fanstatic.TemplateEngine;

/// <summary>
/// Wraps a failure raised while rendering a template and locates it in the template source.
/// Fluid reports no position for runtime failures, so the reference is matched against the template body
/// to point at the tag that asked for the missing partial or base template.
/// </summary>
public sealed class TemplateRenderException : Exception
{
    /// <summary>
    /// ctr
    /// </summary>
    TemplateRenderException(string message, int line, int column, string? sourceExcerpt, Exception innerException)
        : base(message, innerException)
    {
        Line = line;
        Column = column;
        SourceExcerpt = sourceExcerpt;
    }

    /// <summary>
    /// One-based line of the failing tag, 0 when the position is unknown.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// One-based column of the failing tag, 0 when the position is unknown.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Source line holding the failing tag, null when the position is unknown.
    /// </summary>
    public string? SourceExcerpt { get; }

    /// <summary>
    /// Describes <paramref name="missingTemplate"/> as not found at the tag that references it.
    /// </summary>
    /// <param name="missingTemplate">The partial or base template Fluid could not load.</param>
    /// <param name="templateBody">Body of the template that referenced it.</param>
    /// <param name="innerException">The failure thrown by Fluid.</param>
    public static TemplateRenderException MissingTemplate(string missingTemplate, string templateBody,
        Exception innerException)
    {
        ArgumentNullException.ThrowIfNull(missingTemplate);
        ArgumentNullException.ThrowIfNull(templateBody);
        ArgumentNullException.ThrowIfNull(innerException);

        var (line, column) = LocateTagReferencing(templateBody, missingTemplate);
        var excerpt = line > 0 ? SourceLineAt(templateBody, line) : null;

        return new TemplateRenderException($"template not found: '{missingTemplate}'", line, column, excerpt,
            innerException);
    }

    static (int Line, int Column) LocateTagReferencing(string templateBody, string reference)
    {
        var referenceIndex = templateBody.IndexOf(reference, StringComparison.Ordinal);
        if (referenceIndex < 0)
        {
            return (0, 0);
        }

        var tagIndex = templateBody.LastIndexOf(TagStart, referenceIndex, StringComparison.Ordinal);
        return tagIndex < 0 ? (0, 0) : PositionAt(templateBody, tagIndex);
    }

    static (int Line, int Column) PositionAt(string templateBody, int index)
    {
        var line = 1;
        var lineStart = 0;

        for (var i = 0; i < index; i++)
        {
            if (templateBody[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return (line, index - lineStart + 1);
    }

    static string? SourceLineAt(string templateBody, int line)
    {
        using var reader = new StringReader(templateBody);

        for (var current = 1; current < line; current++)
        {
            if (reader.ReadLine() is null)
            {
                return null;
            }
        }

        return reader.ReadLine()?.Trim();
    }

    const string TagStart = "{%";
}
