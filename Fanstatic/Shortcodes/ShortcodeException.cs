namespace Fanstatic.Shortcodes;

/// <summary>
/// A shortcode syntax, resolution or render failure, located in the content file that calls the shortcode.
/// </summary>
public sealed class ShortcodeException : Exception
{
    /// <summary>
    /// Creates a failure located at a character offset of the content being processed.
    /// </summary>
    public ShortcodeException(string message, int offset, Exception? innerException = null)
        : base(message, innerException)
    {
        Offset = offset;
    }

    ShortcodeException(ShortcodeException source, string sourcePath, int line, int column, string? excerpt)
        : base(source.Message, source.InnerException)
    {
        Offset = source.Offset;
        SourcePath = sourcePath;
        Line = line;
        Column = column;
        SourceExcerpt = excerpt;
    }

    /// <summary>
    /// Zero-based character offset of the failure in the processed content.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// Full path of the content file, empty until <see cref="WithLocation"/> is applied.
    /// </summary>
    public string SourcePath { get; } = string.Empty;

    /// <summary>
    /// One-based line in the content file, 0 when unknown.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// One-based column in the content file, 0 when unknown.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// The source line holding the failing tag.
    /// </summary>
    public string? SourceExcerpt { get; }

    /// <summary>
    /// Returns a copy of this failure located in a content file.
    /// </summary>
    public ShortcodeException WithLocation(string sourcePath, int line, int column, string? excerpt) =>
        new(this, sourcePath, line, column, excerpt);
}
