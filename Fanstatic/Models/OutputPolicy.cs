namespace Fanstatic.Models;

/// <summary>
/// Specifies how to handle an existing output directory
/// </summary>
public enum OutputPolicy
{
    /// <summary>
    /// Delete the existing directory and create a new one
    /// </summary>
    delete,

    /// <summary>
    /// Throw an exception if the directory exists
    /// </summary>
    fail,

    /// <summary>
    /// Keep the existing directory and overwrite files as needed (default)
    /// </summary>
    overwrite
}
