using Fanstatic.Models;

namespace Fanstatic.Helpers;

/// <summary>
/// Key used for template cache dictionaries.
/// </summary>
/// <param name="Section">Page section.</param>
/// <param name="Kind">Page kind flags.</param>
/// <param name="Type">Page type.</param>
/// <param name="OutputFormat">Output format name.</param>
public readonly record struct CacheTemplateIndex(
    string? Section,
    Kind? Kind,
    string? Type,
    string OutputFormat);
