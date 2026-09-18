namespace Fanstatic.Commands.API.APIModels;

/// <summary>
/// Represents a documentation parameter with a name and description.
/// </summary>
/// <param name="Name">The name of the associated entity, parameter, or element.</param>
/// <param name="Description">The descriptive text or details for an associated entity or element.</param>
public record ParamDoc(
    string Name = "",
    string Description = ""
);
