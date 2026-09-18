namespace Fanstatic.Commands.API.APIModels;

/// <summary>
/// Represents information about a property within a class or struct, including name, type,
/// access modifiers, whether it has a getter or setter, default value, and documentation details.
/// </summary>
public record PropertyInfo(
    string Name = "",
    string Type = "",
    string Modifiers = "",
    bool HasGetter = false,
    bool HasSetter = false,
    string? DefaultValue = null,
    DocumentationInfo? Documentation = null
)
{
    /// <summary>
    /// Gets the documentation information associated with the property.
    /// </summary>
    /// <remarks>
    /// This property provides detailed documentation for the property, including
    /// a summary, parameters, return descriptions, examples, and additional remarks.
    /// </remarks>
    /// <value>
    /// An instance of <see cref="DocumentationInfo"/> containing the documentation details.
    /// </value>
    public DocumentationInfo Documentation { get; init; } = Documentation ?? new();
}
