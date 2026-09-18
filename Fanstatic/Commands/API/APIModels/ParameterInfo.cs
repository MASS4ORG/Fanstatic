namespace Fanstatic.Commands.API.APIModels;

/// <summary>
/// Represents information about a method parameter.
/// </summary>
/// <param name="Name">The name of the parameter.</param>
/// <param name="Type">The data type of the parameter.</param>
/// <param name="DefaultValue">The default value of the parameter, if any.</param>
public record ParameterInfo(
    string Name = "",
    string Type = "",
    string? DefaultValue = null
);
