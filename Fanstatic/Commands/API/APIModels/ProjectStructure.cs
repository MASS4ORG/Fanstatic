namespace Fanstatic.Commands.API.APIModels;

/// <summary>
/// Represents the structure of a project, containing information about namespaces
/// and their associated classes, as well as a collection of all the classes within the project.
/// </summary>
/// <param name="NamespaceClasses">A dictionary that organizes classes into their respective namespaces.</param>
/// <param name="AllClasses">A collection of all classes identified within a project.</param>
public record ProjectStructure(
    Dictionary<string, List<ClassInfo>>? NamespaceClasses = null,
    List<ClassInfo>? AllClasses = null
)
{
    /// <summary>
    /// Represents a dictionary that organizes classes into their respective namespaces.
    /// </summary>
    /// <remarks>
    /// The property serves as a mapping where the keys are namespace names as strings,
    /// and the values are lists of <c>ClassInfo</c> objects that detail classes within that namespace.
    /// It facilitates structured access to classes grouped by their corresponding namespace.
    /// </remarks>
    public Dictionary<string, List<ClassInfo>> NamespaceClasses { get; init; } = NamespaceClasses ?? new();

    /// <summary>
    /// Represents a collection of all classes identified within a project.
    /// </summary>
    /// <remarks>
    /// This property stores a list of <c>ClassInfo</c> objects, each containing detailed metadata
    /// about a specific class, such as its name, namespace, type kind, and other associated information.
    /// It provides a centralized repository for access to all class-level structures found in the analyzed project.
    /// </remarks>
    public List<ClassInfo> AllClasses { get; init; } = AllClasses ?? new();
}
