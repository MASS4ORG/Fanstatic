namespace Fanstatic.Models;

/// <summary>
/// A Markdown heading collected from a page, with its generated HTML identifier.
/// </summary>
/// <param name="Level">Heading level, starting at 1.</param>
/// <param name="Text">Plain text of the heading.</param>
/// <param name="Id">Identifier generated for the heading by Markdig.</param>
public sealed record Heading(int Level, string Text, string Id);
