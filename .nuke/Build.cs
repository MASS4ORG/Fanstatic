namespace Build;

/// <summary>
/// This is the main build file for the project.
/// </summary>
[ShutdownDotNetAfterServerBuild]
internal sealed partial class Build : NukeBuild
{
    private static int Main() => Execute<Build>(x => x.Compile);
}
