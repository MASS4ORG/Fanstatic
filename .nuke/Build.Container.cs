using Cri = (string identifier, string family);

namespace Fanstatic.NUKE;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for building and pushing the OCI container image.
/// The .NET SDK produces and pushes the image directly, so no Docker/Podman daemon is required.
/// </summary>
sealed partial class Build
{
    [Parameter("Comma-separated image repositories to tag/push, e.g. ghcr.io/mass4org/fanstatic")]
    readonly string ContainerRegistries = "";

    [Parameter("Registry user used to authenticate the container push (example: gitlab-ci-token)")]
    readonly string ContainerRegistryUser;

    [Parameter("Registry password/token used to authenticate the container push")]
    [Secret]
    readonly string ContainerRegistryPassword;

    string[] ContainerAllRegistries =>
        ContainerRegistries.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [Parameter("Default runtime that also receives generic tags")]
    readonly string ContainerDefaultRid = "linux-x64";

    Cri? ContainerRuntimeIdentifier => RuntimeIdentifier switch
    {
        "linux-x64" => ("linux-x64", "noble-chiseled"),
        "linux-arm64" => ("linux-arm64", "noble-chiseled"),
        "linux-musl-x64" => ("alpine", "alpine"),
        _ => null,
    };

    /// <summary>
    /// Throwaway path for the image archive produced by <see cref="ContainerBuild"/>.
    /// </summary>
    AbsolutePath ContainerArchive =>
        PublishDir / $"fanstatic-{RuntimeIdentifier}.tar.gz";

    /// <summary>
    /// Builds the container image to a throwaway archive on every commit, only to verify the release image
    /// still builds. It runs the exact same build as <see cref="ContainerPush"/> but writes a tarball
    /// instead of pushing, so it needs no container daemon and runs on any runner. The archive is discarded.
    /// </summary>
    public Target ContainerBuild => td => td
        .DependsOn(Restore)
        .OnlyWhenStatic(() => ContainerRuntimeIdentifier is not null)
        .Executes(() => ContainerPublish(null));

    /// <summary>
    /// Pushes the image built by <see cref="ContainerBuild"/> to every repository listed in
    /// <see cref="ContainerAllRegistries"/>, tagged with both the release version and the generic tags.
    /// The CI job is responsible for supplying registry credentials beforehand.
    /// </summary>
    public Target ContainerPush => td => td
        .DependsOn(ContainerBuild)
        .Requires(() => ContainerRegistries)
        .OnlyWhenStatic(() => ContainerRuntimeIdentifier is not null)
        .Executes(() =>
        {
            foreach (var registry in ContainerAllRegistries)
            {
                var separator = registry.IndexOf('/');
                if (separator < 1)
                {
                    throw new InvalidOperationException(
                        $"'{registry}' is not a registry-qualified repository (expected e.g. ghcr.io/mass4org/fanstatic).");
                }

                ContainerPublish((registry[..separator], registry[(separator + 1)..].ToLowerInvariant()));
            }
        });

    /// <summary>
    /// Runs the .NET SDK container publish, either writing a throwaway archive (no registry) or pushing to
    /// the given registry host and repository path.
    /// </summary>
    void ContainerPublish((string host, string path)? registry)
    {
        var cri = ContainerRuntimeIdentifier!.Value;

        // NUKE currently requires the surrounding spaces/quotes so the semicolon-separated list reaches MSBuild intact.
        var tags = " \"" + string.Join(";", ContainerTags()) + "\" ";

        DotNetPublish(settings =>
        {
            settings = settings
                .SetProject(Solution.Fanstatic)
                .SetConfiguration(Config)
                .SetOutput(PublishDir)
                .SetRuntime(RuntimeIdentifier)
                .SetSelfContained(PublishSelfContained)
                .SetPublishSingleFile(PublishSingleFile)
                .SetPublishTrimmed(PublishTrimmed)
                .SetPublishReadyToRun(PublishReadyToRun)
                .SetVersion(Version)
                .SetAssemblyVersion(Version)
                .SetInformationalVersion(Version)
                .SetProperty("TrimMode", "partial")
                .SetProperty("EnableTrimAnalyzer", PublishTrimmed)
                .SetProperty("EnableCompressionInSingleFile", "true")
                .SetProperty("EnableSdkContainerSupport", "true")
                .SetProperty("ContainerAppCommandInstruction", "None")
                .SetProperty("ContainerWorkingDirectory", "/bin")
                .SetProperty("ContainerImageTags", tags)
                .SetProperty("ContainerFamily", cri.family)
                // The SDK only produces/pushes the image when this target runs;
                // a plain publish would skip container creation entirely.
                .SetProcessAdditionalArguments("-target:PublishContainer");

            if (registry is not ({ } host, { } path))
            {
                // No registry: write the image to a throwaway archive instead of loading it into a local
                // daemon, so no engine is needed.
                return settings.SetProperty("ContainerArchiveOutputPath", ContainerArchive);
            }

            return settings
                .SetProperty("ContainerRegistry", host)
                // Registries reject a repository path containing the host, so the caller passes it split.
                .SetProperty("ContainerRepository", path)
                .SetProcessEnvironmentVariable("SDK_CONTAINER_REGISTRY_UNAME", ContainerRegistryUser ?? string.Empty)
                .SetProcessEnvironmentVariable("SDK_CONTAINER_REGISTRY_PWORD", ContainerRegistryPassword ?? string.Empty);
        });
    }

    /// <summary>
    /// Returns all tags that should be applied to the generated image.
    /// </summary>
    List<string> ContainerTags()
    {
        var cri = ContainerRuntimeIdentifier!.Value;
        var localTag = IsLocalBuild ? "local" : string.Empty;

        var genericTags = new[]
            {
                Version,
                VersionMajorMinor,
                VersionMajor,
                string.Empty
            }
            .Select(tag =>
                string.IsNullOrEmpty(localTag) || string.IsNullOrEmpty(tag)
                    ? $"{localTag}{tag}"
                    : $"{localTag}-{tag}")
            .ToList();

        var runtimeTags = genericTags
            .Select(tag =>
                string.IsNullOrEmpty(tag)
                    ? cri.identifier
                    : $"{tag}-{cri.identifier}")
            .ToList();

        // Non-default runtimes receive only RID-qualified tags.
        if (RuntimeIdentifier != ContainerDefaultRid)
        {
            return [.. runtimeTags
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()];
        }

        // The default runtime also receives generic tags (latest, major, etc.).
        genericTags.Add(
            string.IsNullOrEmpty(localTag)
                ? cri.identifier
                : $"{localTag}-{cri.identifier}");

        runtimeTags.AddRange(genericTags);

        return [.. runtimeTags
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()];
    }
}
