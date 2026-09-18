using System.Reflection;

namespace Fanstatic.Models;

/// <summary>
/// Fanstatic internals
/// </summary>
public class FanstaticInfo
{
    /// <summary>
    /// Return true if the `Fanstatic serve` is running.
    /// </summary>
    public bool IsServer { get; set; }

    /// <summary>
    /// The .NET version.
    /// </summary>
    public Version DotNetVersion => Environment.Version;

    /// <summary>
    /// The Fanstatic version.
    /// </summary>
    public Version? Version => Assembly.GetExecutingAssembly().GetName().Version;

    /// <summary>
    /// The date and time that the app was compiled.
    /// </summary>
    public DateTime BuildDate => FanstaticExt.BuildDate;
}
