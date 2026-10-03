namespace Fanstatic.NUKE;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the publish process.
/// </summary>
sealed partial class Build
{
    [Parameter(
        "Runtime identifier for the build (e.g., win-x64, linux-x64, osx-x64) (default: linux-x64)"
    )]
    readonly string RuntimeIdentifier = "linux-x64";

    [Parameter("publish-directory (default: ./.publish/{runtimeIdentifier})")]
    readonly AbsolutePath PublishDirectory;

    AbsolutePath PublishDir => PublishDirectory ?? RootDirectory / ".publish" / RuntimeIdentifier;

    [Parameter("publish-self-contained (default: true)")]
    readonly bool PublishSelfContained = true;

    [Parameter("publish-single-file (default: true)")]
    readonly bool PublishSingleFile = true;

    [Parameter("publish-trimmed (default: true)")]
    readonly bool PublishTrimmed = true;

    [Parameter("publish-ready-to-run (default: false)")]
    readonly bool PublishReadyToRun;

    Target Publish => td =>
        td
            .DependsOn(Restore)
            .Executes(() =>
            {
                _ = DotNetPublish(settings => settings
                    .SetNoLogo(true)
                    .SetProject(Solution.Fanstatic)
                    .SetConfiguration(Config)
                    .SetOutput(PublishDir)
                    .SetRuntime(RuntimeIdentifier)
                    .SetSelfContained(PublishSelfContained)
                    .SetPublishSingleFile(PublishSingleFile)
                    .SetPublishReadyToRun(PublishReadyToRun)
                    .SetPublishTrimmed(PublishTrimmed)
                    .SetVersion(Version)
                    .SetAssemblyVersion(Version)
                    .SetInformationalVersion(Version)
                    .SetProperty("TrimMode", "partial")
                    .SetProperty("EnableTrimAnalyzer", PublishTrimmed)
                    .SetProperty("EnableCompressionInSingleFile", "true")
                );
            });
}
