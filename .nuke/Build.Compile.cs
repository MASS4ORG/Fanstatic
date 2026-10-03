namespace Fanstatic.NUKE;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the build process.
/// </summary>
sealed partial class Build
{
    Target Clean => td => td
        .Executes(() =>
        {
            Solution.AllProjects
                .Where(project => project.Path != RootDirectory / ".nuke/Build.csproj")
                .SelectMany(project => new[]
                {
                    project.Directory / "bin",
                    project.Directory / "obj",
                    project.Directory / "output",
                })
                .Distinct()
                .Where(path => path.DirectoryExists())
                .ForEach(path => path.DeleteDirectory());
            PublishDir.DeleteDirectory();
            CoverageDirectory.DeleteDirectory();
        });

    Target Restore => td => td
        .DependsOn(Clean)
        .Executes(() => _ = DotNetRestore(s => s.SetProjectFile(Solution)));

    Target Compile => td => td
        .DependsOn(Restore)
        .Executes(() =>
        {
            Log.Debug("Config {Config}", Config);

            _ = DotNetBuild(settings => settings
                .SetNoLogo(true)
                .SetProjectFile(Solution)
                .SetConfiguration(Config)
                .EnableNoRestore()
            );
        });
}
