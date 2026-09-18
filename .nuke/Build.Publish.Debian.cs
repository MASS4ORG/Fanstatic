namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the publish debian package.
/// </summary>
partial class Build
{
    private string DebianArch => RuntimeToDebianArch.TryGetValue(RuntimeIdentifier, out var arch) ? arch : "amd64";
    private AbsolutePath DebianPackage => PublishDir / $"fanstatic-{DebianArch}.deb";

    // Supported Debian architectures mapping
    private static readonly Dictionary<string, string> RuntimeToDebianArch = new()
    {
        { "linux-x64", "amd64" },
        { "linux-arm64", "arm64" }
    };

    public Target CreateDebianPackage => td => td
        .After(Publish)
        .OnlyWhenStatic(() => RuntimeToDebianArch.ContainsKey(RuntimeIdentifier))
        .Executes(() =>
        {
            var debianPath = Solution.Build.Directory / "Debian";
            var fanstaticPath = debianPath / "usr" / "local" / "bin" / "fanstatic";
            var debianControlFile = debianPath / "DEBIAN" / "control";
            var debianCopyrightFile = debianPath / "usr" / "share" / "doc" / "fanstatic" / "copyright";
            var licenseSource = RootDirectory / "LICENSE.md";

            // Copy binary
            (PublishDir / "fanstatic").Copy(fanstaticPath, ExistsPolicy.FileOverwrite);

            // Ensure the binary is executable
            ProcessTasks.StartProcess("chmod", $"755 {fanstaticPath}")
                .AssertZeroExitCode();

            // Copy license
            if (licenseSource.FileExists())
            {
                debianCopyrightFile.Parent.CreateDirectory();
                licenseSource.Copy(debianCopyrightFile, ExistsPolicy.FileOverwrite);
            }

            // Generate control file dynamically from csproj metadata
            var controlContent = GenerateControlFile();
            debianControlFile.WriteAllText(controlContent);
            ProcessTasks.StartProcess("chmod", $"644 {debianControlFile}")
                .AssertZeroExitCode();

            ProcessTasks.StartProcess("dpkg-deb", $"--build --root-owner-group {debianPath} {DebianPackage}")
                .AssertZeroExitCode();
        });

    private string GenerateControlFile()
    {
        var arch = DebianArch;
        var project = Solution.Fanstatic.GetMSBuildProject();
        var author = project.GetProperty("Authors")?.EvaluatedValue ?? "Bruno Massa";
        var homepage = project.GetProperty("PackageProjectUrl")?.EvaluatedValue ?? "https://fanstatic.mass4.com";
        var description = "Fanstatic is for Fans of a Fantastic Static Site Generator.";
        var longDescription = "Fanstatic is for Fans of a Fantastic Static Site Generator";

        // Debian requirement: Long description lines MUST start with a space
        longDescription = string.Join("\n",
            longDescription.Split('\n').Select(line => " " + line.Trim()));

        return $"""
                Package: fanstatic
                Version: {VersionFull}
                Architecture: {arch}
                Section: web
                Priority: optional
                Maintainer: {author} <massa+fanstatic@brunomassa.com>
                Standards-Version: 4.6.1
                Homepage: {homepage}
                Description: {description}
                {longDescription}
                """ + "\n";
    }
}
