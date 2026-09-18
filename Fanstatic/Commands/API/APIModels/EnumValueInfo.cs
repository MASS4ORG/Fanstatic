namespace Fanstatic.Commands.API.APIModels;

/// <summary>
/// Represents metadata information for an enumeration value, including its name,
/// optional value, and associated documentation details.
/// </summary>
public record EnumValueInfo(
    string Name = "",
    string? Value = null,
    DocumentationInfo? Documentation = null
)
{
    /// <summary>
    /// Gets or initializes the documentation information associated with an enum value.
    /// Provides details such as summary, parameters, return type, examples, remarks, and raw content
    /// about the associated enum value.
    /// </summary>
    public DocumentationInfo Documentation { get; init; } = Documentation ?? new();
}
