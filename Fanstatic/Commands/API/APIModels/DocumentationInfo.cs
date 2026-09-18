namespace Fanstatic.Commands.API.APIModels;

/// <summary>
/// Represents comprehensive documentation details for an element, including summary, parameters,
/// return information, example usage, remarks, and raw documentation content.
/// </summary>
public record DocumentationInfo(
    string Summary = "",
    List<ParamDoc>? Parameters = null,
    string Returns = "",
    string Example = "",
    string Remarks = "",
    string RawContent = ""
)
{
    /// <summary>
    /// Gets or sets the collection of parameter documentation associated with
    /// a method or property. This collection typically contains information
    /// about the name and description of each parameter involved.
    /// </summary>
    public List<ParamDoc> Parameters { get; init; } = Parameters ?? new();
}
