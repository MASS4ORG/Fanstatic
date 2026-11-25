using YamlDotNet.Serialization;

namespace Fanstatic.Models;

/// <summary>
/// Heading levels included in the generated table of contents.
/// </summary>
[YamlSerializable]
public class TableOfContentsSettings
{
    /// <summary>
    /// Lowest heading level included in the table of contents.
    /// </summary>
    public int StartLevel { get; set; } = 2;

    /// <summary>
    /// Highest heading level included in the table of contents.
    /// </summary>
    public int EndLevel { get; set; } = 3;
}
