using Fanstatic.Helpers;
using Fanstatic.Parsers;
using YamlDotNet.Serialization;

namespace Fanstatic.Models;

/// <summary>
/// A scaffold structure to help creating system-generated content, like
/// tag, section or index pages
/// </summary>
[YamlSerializable]
public class FrontMatter : IFrontMatter
{
    static readonly HashSet<string> KnownFields = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(Title),
        nameof(Description),
        nameof(Type),
        nameof(Url),
        nameof(Draft),
        nameof(Aliases),
        nameof(Section),
        nameof(Date),
        nameof(LastMod),
        nameof(PublishDate),
        nameof(ExpiryDate),
        nameof(Weight),
        nameof(Tags),
        nameof(ResourceDefinitions),
        nameof(Params),
        nameof(Cascade)
    };

    internal Dictionary<string, object> AdditionalFields { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    #region IFrontMatter

    /// <inheritdoc/>
    public string? Title { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string? Description { get; set; }

    /// <inheritdoc/>
    public string? Type { get; set; } = "page";

    /// <inheritdoc/>
    public string? Url { get; set; }

    /// <inheritdoc/>
    public bool? Draft { get; set; }

    /// <inheritdoc/>
    public List<string>? Aliases { get; set; }

    /// <inheritdoc/>
    public string? Section { get; set; } = string.Empty;

    /// <inheritdoc/>
    public DateTime? Date { get; set; }

    /// <inheritdoc/>
    public DateTime? LastMod { get; set; }

    /// <inheritdoc/>
    public DateTime? PublishDate { get; set; }

    /// <inheritdoc/>
    public DateTime? ExpiryDate { get; set; }

    /// <inheritdoc/>
    public int Weight { get; set; }

    /// <inheritdoc/>
    public List<string>? Tags { get; set; }

    /// <inheritdoc/>
    public List<FrontMatterResources>? ResourceDefinitions { get; set; }

    /// <inheritdoc/>
    public Dictionary<string, object> Params { get; set; } = [];

    /// <summary>
    /// Cascade front matter data to its children.
    /// </summary>
    public FrontMatter? Cascade { get; set; }

    #endregion IFrontMatter

    /// <summary>
    /// Constructor
    /// </summary>
    public FrontMatter()
    {
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="title"></param>
    public FrontMatter(string title)
    {
        Title = title;
    }

    /// <summary>
    /// Create a front matter from a given front matter + content
    /// </summary>
    /// <param name="frontMatterString"></param>
    /// <param name="fileFullPath"></param>
    /// <param name="fileRelativePath"></param>
    /// <param name="parser"></param>
    /// <returns></returns>
    public static FrontMatter Parse(
        string frontMatterString,
        in string fileFullPath,
        in string fileRelativePath,
        IFrontMatterParser parser
    )
    {
        ArgumentNullException.ThrowIfNull(fileFullPath);
        ArgumentNullException.ThrowIfNull(fileRelativePath);
        ArgumentNullException.ThrowIfNull(parser);

        var frontMatter = parser.Parse<FrontMatter>(frontMatterString);
        if (!string.IsNullOrWhiteSpace(frontMatterString))
        {
            var fields = parser.Parse<Dictionary<string, object>>(frontMatterString);
            foreach (var (key, value) in fields)
            {
                if (!KnownFields.Contains(key))
                {
                    frontMatter.AdditionalFields[key] = value;
                }
            }
        }

        var section = SiteHelper.GetSection(fileRelativePath);
        frontMatter.Section = section;
        frontMatter.Type ??= section;
        return frontMatter;
    }

    /// <summary>
    /// Create a front matter from a given content
    /// </summary>
    /// <param name="fileFullPath"></param>
    /// <param name="fileRelativePath"></param>
    /// <param name="parser"></param>
    /// <param name="content"></param>
    /// <returns></returns>
    public static (FrontMatter, string) Parse(
        in string fileFullPath,
        in string fileRelativePath,
        IFrontMatterParser parser,
        string content
    )
    {
        ArgumentNullException.ThrowIfNull(fileFullPath);
        ArgumentNullException.ThrowIfNull(fileRelativePath);
        ArgumentNullException.ThrowIfNull(parser);

        var (frontMatter, rawContent) = parser.SplitFrontMatterAndContent(content);
        return (Parse(frontMatter, fileFullPath, fileRelativePath, parser), rawContent);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="other"></param>
    public FrontMatter Merge(FrontMatter other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var merged = new FrontMatter();
        MergeIdentity(other, merged);
        MergeDates(other, merged);
        MergeCollections(other, merged);
        merged.MergeAdditionalFields(other);
        merged.MergeAdditionalFields(this, overwrite: false);
        return merged;
    }

    void MergeIdentity(FrontMatter other, FrontMatter merged)
    {
        MergeTitles(other, merged);
        MergeTypeAndLocation(other, merged);
    }

    void MergeTitles(FrontMatter other, FrontMatter merged)
    {
        merged.Title = string.IsNullOrEmpty(other.Title) ? Title : other.Title;
        merged.Description = string.IsNullOrEmpty(other.Description) ? Description : other.Description;
    }

    void MergeTypeAndLocation(FrontMatter other, FrontMatter merged)
    {
        merged.Type = string.IsNullOrEmpty(other.Type) || other.Type == "page" ? Type : other.Type;
        merged.Url = string.IsNullOrEmpty(other.Url) ? Url : other.Url;
        merged.Draft = other.Draft ?? Draft;
        merged.Section = string.IsNullOrEmpty(other.Section) ? Section : other.Section;
    }

    void MergeDates(FrontMatter other, FrontMatter merged)
    {
        merged.Date = other.Date ?? Date;
        merged.LastMod = other.LastMod ?? LastMod;
        merged.PublishDate = other.PublishDate ?? PublishDate;
        merged.ExpiryDate = other.ExpiryDate ?? ExpiryDate;
        merged.Weight = other.Weight != 0 ? other.Weight : Weight;
    }

    void MergeCollections(FrontMatter other, FrontMatter merged)
    {
        merged.Aliases = other.Aliases ?? Aliases;
        merged.Tags = other.Tags ?? Tags;
        merged.ResourceDefinitions = other.ResourceDefinitions ?? ResourceDefinitions;
        merged.Params = other.Params.Count != 0 ? other.Params : Params;
        merged.Cascade = other.Cascade ?? Cascade;
    }

    internal void MergeAdditionalFields(FrontMatter other, bool overwrite = true)
    {
        foreach (var (key, value) in other.AdditionalFields)
        {
            if (overwrite || !AdditionalFields.ContainsKey(key))
            {
                AdditionalFields[key] = value;
            }
        }
    }
}
