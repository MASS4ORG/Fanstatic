using YamlDotNet.Serialization;

namespace Fanstatic.Models;

/// <summary>
/// Per-language configuration, as defined under the <c>languages</c> key of the
/// site settings file. Values not set here fall back to the root site settings.
/// </summary>
[YamlSerializable]
public class LanguageSettings : IParams
{
    /// <summary>
    /// The language code (e.g. <c>en</c>, <c>pt-br</c>). Filled in from the
    /// <c>languages</c> dictionary key, so it is not read from the YAML body.
    /// </summary>
    [YamlIgnore]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Language-specific site title. Falls back to the root title when null.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Language-specific site description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The human-readable name of the language (e.g. <c>English</c>,
    /// <c>Português</c>), typically used in language switchers.
    /// </summary>
    public string? LanguageName { get; set; }

    /// <summary>
    /// Text direction of the language, <c>ltr</c> (default) or <c>rtl</c>.
    /// </summary>
    public string LanguageDirection { get; set; } = "ltr";

    /// <summary>
    /// Ordering weight used to sort the list of languages.
    /// </summary>
    public int Weight { get; set; }

    /// <summary>
    /// Language-specific base URL. Falls back to the root base URL when null.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    /// True when this is the site's default language.
    /// </summary>
    [YamlIgnore]
    public bool IsDefault { get; set; }

    /// <summary>
    /// The relative URL of this language's home page (e.g. <c>/</c> for the default
    /// language, <c>/pt-br/</c> otherwise). Useful as a fallback target in language
    /// switchers when the current page has no translation in a given language.
    /// </summary>
    [YamlIgnore]
    public Uri RelPermalink { get; set; } = new("/", UriKind.Relative);

    #region IParams

    /// <inheritdoc/>
    public Dictionary<string, object> Params { get; set; } = [];

    #endregion IParams
}
