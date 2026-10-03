namespace Fanstatic.NUKE;

/// <summary>Builds a local Flatpak bundle for the static site generator.</summary>
sealed partial class Build
{
    const string FlatpakAppId = "org.MASS4.Fanstatic";
    AbsolutePath FlatpakManifest => Solution.Build.Directory / "packaging" / "flatpak" / $"{FlatpakAppId}.yml";
    AbsolutePath FlatpakBuildDirectory => RootDirectory / ".flatpak-build";
    AbsolutePath FlatpakRepository => RootDirectory / ".flatpak-repo";
    AbsolutePath FlatpakBundle => ArtifactsDirectory / $"Fanstatic-{Version}-x86_64.flatpak";

    public Target Flatpak => td => td
        .Executes(() =>
        {
            FlatpakBuildDirectory.DeleteDirectory();
            FlatpakRepository.CreateDirectory();
            ArtifactsDirectory.CreateDirectory();
            FlatpakBundle.DeleteFile();
            ProcessTasks.StartProcess("flatpak-builder",
                    $"--force-clean --repo=\"{FlatpakRepository}\" --install-deps-from=flathub " +
                    $"\"{FlatpakBuildDirectory}\" \"{FlatpakManifest}\"")
                .AssertZeroExitCode();
            ProcessTasks.StartProcess("flatpak",
                    $"build-bundle \"{FlatpakRepository}\" \"{FlatpakBundle}\" {FlatpakAppId}")
                .AssertZeroExitCode();
        });
}
