namespace Build;

sealed partial class Build
{
    string PublishBenchmarkReference(string tempDirectory, string version)
    {
        var referenceDirectory = Path.Combine(tempDirectory, "reference");
        var archive = Path.Combine(tempDirectory, "reference.zip");
        var tag = $"v{version}";
        Log.Information("Publishing {Tag} to remeasure the reference on this runner", tag);
        _ = Git($"archive --format=zip --output=\"{archive}\" {tag}", workingDirectory: RootDirectory);
        ZipFile.ExtractToDirectory(archive, referenceDirectory);

        var relativeProject = Path.GetRelativePath(RootDirectory, ProductProject.Path);
        var outputDirectory = Path.Combine(tempDirectory, "reference-publish");
        _ = DotNetPublish(settings => settings
            .SetProcessWorkingDirectory(RootDirectory)
            .SetNoLogo(true)
            .SetProject(Path.Combine(referenceDirectory, relativeProject))
            .SetConfiguration(ConfigurationOptions.Release)
            .SetOutput(outputDirectory)
            .SetRuntime(RuntimeIdentifier)
            .SetSelfContained(false)
            .SetPublishSingleFile(false)
            .SetPublishTrimmed(false));

        var executable = Path.Combine(outputDirectory,
            OperatingSystem.IsWindows() ? $"{ExecutableName}.exe" : ExecutableName);
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException("The published reference executable was not found.", executable);
        }

        Log.Information("Comparing with {Tag} measured on this runner in alternating order", tag);
        return executable;
    }
}
