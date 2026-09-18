namespace Fanstatic.Commands.API.APIModels;

/// <summary>
/// Represents information about a method within a class or struct, including its name, return type, modifiers, parameters, and associated documentation.
/// </summary>
public record MethodInfo(
    string Name = "",
    string ReturnType = "",
    string Modifiers = "",
    List<ParameterInfo>? Parameters = null,
    DocumentationInfo? Documentation = null
)
{
    /// <summary>
    /// Represents the collection of parameters associated with the method.
    /// Provides detailed information about each parameter including its name, type, and optional default value.
    /// </summary>
    public List<ParameterInfo> Parameters { get; init; } = Parameters ?? new();

    /// <summary>
    /// Provides documentation details for the associated method such as summary, parameters, return value,
    /// usage example, remarks, and raw content of the documentation.
    /// </summary>
    public DocumentationInfo Documentation { get; init; } = Documentation ?? new();
}
