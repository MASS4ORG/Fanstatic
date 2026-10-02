namespace Fanstatic.NUKE;

/// <summary>
/// Produces an independently installable Debian package for the published command-line executable.
/// </summary>
sealed partial class Build
{
    static readonly IReadOnlyDictionary<string, string> DebianArchitectures =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["linux-x64"] = "amd64",
            ["linux-arm64"] = "arm64"
        };

    string DebianArchitecture => DebianArchitectures[RuntimeIdentifier];

    public Target DebianPackage => td => td
        .DependsOn(Publish)
        .OnlyWhenStatic(() => DebianArchitectures.ContainsKey(RuntimeIdentifier))
        .Executes(() =>
        {
            var stagingRoot = (AbsolutePath)Path.Combine(Path.GetTempPath(), $"fanstatic-debian-{RuntimeIdentifier}");
            stagingRoot.DeleteDirectory();

            var binaryDirectory = stagingRoot / "usr" / "bin";
            binaryDirectory.CreateDirectory();
            var binary = binaryDirectory / "fanstatic";
            (PublishDir / "fanstatic").Copy(binary, ExistsPolicy.FileOverwrite);

            ProcessTasks.StartProcess("chmod", $"0755 \"{binary}\"")
                .AssertZeroExitCode();

            WriteDebianMetadata(stagingRoot);
            BuildDebianArchive(stagingRoot);
        });

    void WriteDebianMetadata(AbsolutePath packageRoot)
    {
        var documentationDirectory = packageRoot / "usr" / "share" / "doc" / "fanstatic";
        documentationDirectory.CreateDirectory();
        (RootDirectory / "LICENSE.md").Copy(documentationDirectory / "copyright", ExistsPolicy.FileOverwrite);

        var project = Solution.Fanstatic.GetMSBuildProject();
        var author = project.GetProperty("Authors")?.EvaluatedValue ?? "Bruno Massa";
        var homepage = project.GetProperty("PackageProjectUrl")?.EvaluatedValue ?? "https://fanstatic.mass4.com";

        // A framework-dependent build carries no runtime of its own; a self-contained one needs nothing.
        var dependencies = PublishSelfContained ? string.Empty : Environment.NewLine + "Depends: dotnet-runtime-10.0";
        var summary = "static site generator for fans of a fantastic one";

        (packageRoot / "DEBIAN").CreateDirectory();
        (packageRoot / "DEBIAN" / "control").WriteAllText($"""
            Package: fanstatic
            Version: {Version}
            Architecture: {DebianArchitecture}
            Section: web
            Priority: optional
            Maintainer: {author} <massa+fanstatic@brunomassa.com>
            Standards-Version: 4.6.1
            Homepage: {homepage}{dependencies}
            Description: {summary}
             Fanstatic generates static sites from Markdown with YAML frontmatter.
            """ + "\n");
    }

    void BuildDebianArchive(AbsolutePath packageRoot)
    {
        ArtifactsDirectory.CreateDirectory();
        var packageFile = ArtifactsDirectory / $"fanstatic_{Version}_{DebianArchitecture}.deb";
        packageFile.DeleteFile();
        ProcessTasks.StartProcess("dpkg-deb", $"--build --root-owner-group \"{packageRoot}\" \"{packageFile}\"")
            .AssertZeroExitCode();
    }
}
