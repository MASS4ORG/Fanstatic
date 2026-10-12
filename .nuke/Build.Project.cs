namespace Build;

/// <summary>
/// The only build file that differs between the MASS4 repositories besides the feature partials: it names this
/// project and wires its features into the shared build. Names of projects, repositories and products live here.
/// </summary>
sealed partial class Build
{
    const string ProductName = "Fanstatic";
    const string DefaultGithubRepository = "MASS4ORG/Fanstatic";
    const string DefaultGitlabProjectPath = "MASS4ORG/fanstatic";
    const string ReleaseBotEmail = "massa+github@brunomassa.com";
    const string NuGetPackagePrefix = "MASS4.";
    const string PackageAuthors = "Bruno Massa";
    const string PackageCopyrightOwner = "Bruno Massa";
    const string PackageProjectUrl = "https://fanstatic.mass4.org";
    const string PackageRepositoryUrl = "https://github.com/MASS4ORG/Fanstatic.git";
    const string PackageTags = "static site generator ssg";

    const bool DefaultPublishSelfContained = true;
    const bool DefaultPublishSingleFile = true;
    const bool DefaultPublishTrimmed = true;

    Project ProductProject => Solution.Fanstatic;
    Project TestProject => Solution.Fanstatic_Test;
    IEnumerable<Project> ReleaseProjects => [ProductProject];
    IEnumerable<Project> VersionedProjects => [ProductProject];
    string ExecutableName => ProductProject.Name.ToLowerInvariant();

    AbsolutePath PublishOutput(Project project) => PublishDir;
    Dictionary<string, object> MsBuildProperties => [];
    Target[] PackDependencies => [Publish, WindowsInstaller];
    Target[] ReleaseCommitDependencies => [RecordReleaseBenchmark];

    partial void ConfigurePublish(Project project, ref DotNetPublishSettings settings) =>
        settings = settings
            .SetProperty("TrimMode", "partial")
            .SetProperty("EnableTrimAnalyzer", PublishTrimmed)
            .SetProperty("EnableCompressionInSingleFile", "true");
}
