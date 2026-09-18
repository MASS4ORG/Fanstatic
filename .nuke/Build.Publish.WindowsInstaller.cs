namespace Build;

/// <summary>Builds the Windows installer included with regular Windows releases.</summary>
partial class Build
{
    [Parameter("Path to the NSIS makensis executable (default: makensis)")]
    public readonly string NsisPath = "makensis";

    private AbsolutePath WindowsInstallerFile =>
        PublishDir / $"fanstatic-{VersionFull}-win-x64-setup.exe";

    public Target WindowsInstaller => td => td
        .DependsOn(Publish)
        .OnlyWhenStatic(() => RuntimeIdentifier == "win-x64")
        .Executes(() =>
        {
            WindowsInstallerFile.DeleteFile();
            var script = Solution.Build.Directory / "packaging" / "windows" / "Fanstatic.nsi";
            ProcessTasks.StartProcess(NsisPath,
                    $"-V2 -DVERSION={VersionFull} -DSOURCE_DIR=\"{PublishDir}\" " +
                    $"-DOUTPUT_FILE=\"{WindowsInstallerFile}\" \"{script}\"")
                .AssertZeroExitCode();
        });
}
