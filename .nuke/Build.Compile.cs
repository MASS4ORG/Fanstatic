namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the build process.
/// </summary>
partial class Build
{
    private Target Clean => s => s
        .Executes(() =>
        {
            Solution.Fanstatic.Directory.GlobDirectories("**/bin", "**/obj", "**/output")
                .ForEach((path) => path.DeleteDirectory()
                );
            Solution.Fanstatic_Test.Directory.GlobDirectories("**/bin", "**/obj", "**/output")
                .ForEach((path) => path.DeleteDirectory()
                );
            PublishDir.DeleteDirectory();
            CoverageDirectory.DeleteDirectory();
        });

    private Target Restore => td => td
        .After(Clean)
        .Executes(() =>
        {
            _ = DotNetTasks.DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    private Target Compile => td => td
        .After(Restore)
        .Executes(() =>
        {
            Log.Debug("Configuration {Configuration}", ConfigurationSet);
            Log.Debug("configuration {configuration}", Configuration);
            _ = DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(ConfigurationSet)
                .EnableNoRestore()
            );
        });
}
