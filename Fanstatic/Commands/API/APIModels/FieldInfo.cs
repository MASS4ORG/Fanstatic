namespace Fanstatic.Commands.API.APIModels;

/// <summary>
/// Represents the metadata of a field in a class or struct, including its name,
/// type, modifiers, default value, and associated documentation.
/// </summary>
public record FieldInfo(
    string Name = "",
    string Type = "",
    string Modifiers = "",
    string? DefaultValue = null,
    DocumentationInfo? Documentation = null
)
{
    /// <summary>
    /// Represents the documentation associated with a field, including details
    /// such as summary, parameters, return value, example usage, remarks,
    /// and raw content.
    /// </summary>
    public DocumentationInfo Documentation { get; init; } = Documentation ?? new();
}
