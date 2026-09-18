using YamlDotNet.Serialization;

namespace Fanstatic.Models;

/// <summary>
/// A single translation entry, supporting both simple string values
/// and plural-aware (one/other) format compatible with Hugo.
/// </summary>
[YamlSerializable]
public class I18NEntry
{
    /// <summary>
    /// Singular form, used when count is 1.
    /// </summary>
    [YamlMember(Alias = "one")]
    public string? One { get; set; }

    /// <summary>
    /// Plural form, used when count is not 1.
    /// </summary>
    [YamlMember(Alias = "other")]
    public string? Other { get; set; }
}
