using Cri = (string identifier, string family);

namespace Build;

/// <summary>
/// This partial is responsible for building and publishing OCI container images
/// using the native .NET SDK container support.
/// </summary>
partial class Build
{
    [Parameter("GitLab CI_REGISTRY_IMAGE")]
    public readonly string ContainerRegistryImage;

    private string RegistryImage => ContainerRegistryImage ?? "fanstatic";

    /// <summary>
    /// The image repository without the registry host. When pushing to a remote
    /// registry the SDK rejects a <c>ContainerRepository</c> that contains the
    /// host, so strip it when it matches <see cref="ContainerRegistry"/> (this
    /// lets CI pass the full <c>$CI_REGISTRY_IMAGE</c> unchanged).
    /// </summary>
    private string ContainerRepositoryPath =>
        !string.IsNullOrWhiteSpace(ContainerRegistry) &&
        RegistryImage.StartsWith(ContainerRegistry + "/",
            StringComparison.OrdinalIgnoreCase)
            ? RegistryImage[(ContainerRegistry.Length + 1)..]
            : RegistryImage;

    [Parameter("Default runtime that also receives generic tags")]
    public readonly string ContainerDefaultRid = "linux-x64";

    /// <summary>
    /// Registry host (example: registry.gitlab.com). When set, the image is
    /// pushed there by the SDK. Leave empty to only build a throwaway archive
    /// (see <see cref="CreateContainer"/>).
    /// </summary>
    [Parameter("Container registry host")]
    public readonly string ContainerRegistry;

    private Cri? ContainerRuntimeIdentifier => RuntimeIdentifier switch
    {
        "linux-x64" => ("linux-x64", "noble-chiseled"),
        "linux-musl-x64" => ("alpine", "alpine"),
        _ => null,
    };

    /// <summary>
    /// Throwaway path for the image archive produced by <see cref="CreateContainer"/>.
    /// </summary>
    private AbsolutePath ContainerArchive =>
        PublishDir / $"fanstatic-{RuntimeIdentifier}.tar.gz";

    /// <summary>
    /// Builds the container image to a throwaway archive on every commit, only to
    /// verify the release image still builds. It runs the exact same build as
    /// <see cref="PublishContainer"/> but writes a tarball instead of pushing, so
    /// it needs no Docker/Podman daemon and runs on any runner. The archive is
    /// discarded.
    /// </summary>
    private Target CreateContainer => td => td
        .OnlyWhenStatic(() => ContainerRuntimeIdentifier is not null)
        .Executes(() => PublishContainerImage());

    /// <summary>
    /// Publishes the container image directly to the configured registry.
    /// </summary>
    public Target PublishContainer => td => td
        .OnlyWhenStatic(() => ContainerRuntimeIdentifier is not null)
        .Requires(() => ContainerRegistry)
        .Executes(() => PublishContainerImage(ContainerRegistry));

    private void PublishContainerImage(string registry = null)
    {
        var cri = ContainerRuntimeIdentifier!.Value;

        // NUKE currently requires the surrounding spaces/quotes so the
        // semicolon-separated list reaches MSBuild intact.
        var tags = " \"" + string.Join(";", ContainerTags()) + "\" ";

        DotNetTasks.DotNetPublish(s =>
        {
            s = s
                .SetProject(Solution.Fanstatic)
                .SetConfiguration(ConfigurationSet)
                .SetOutput(PublishDir)
                .SetRuntime(RuntimeIdentifier)
                .SetSelfContained(PublishSelfContained)
                .SetPublishSingleFile(PublishSingleFile)
                .SetPublishTrimmed(PublishTrimmed)
                .SetPublishReadyToRun(PublishReadyToRun)
                .SetVersion(CurrentVersion)
                .SetAssemblyVersion(CurrentVersion)
                .SetInformationalVersion(CurrentVersion)
                .AddProperty("EnableSdkContainerSupport", true)
                .AddProperty("ContainerAppCommandInstruction", "None")
                .AddProperty("ContainerWorkingDirectory", "/bin")
                .AddProperty("ContainerRepository", ContainerRepositoryPath)
                .AddProperty("ContainerImageTags", tags)
                .AddProperty("ContainerFamily", cri.family)
                // The SDK only produces/pushes the image when this target runs;
                // a plain publish would skip container creation entirely.
                .AddProcessAdditionalArguments("-target:PublishContainer");

            if (!string.IsNullOrWhiteSpace(registry))
            {
                // Push straight to the remote registry over HTTP - no Docker or
                // Podman daemon required. The SDK reads credentials from these
                // env vars; in GitLab CI the job token authenticates the push.
                s = s.AddProperty("ContainerRegistry", registry);

                if (GitLab is not null && !string.IsNullOrEmpty(GitLab.JobToken))
                {
                    s = s
                        .SetProcessEnvironmentVariable(
                            "SDK_CONTAINER_REGISTRY_UNAME", "gitlab-ci-token")
                        .SetProcessEnvironmentVariable(
                            "SDK_CONTAINER_REGISTRY_PWORD", GitLab.JobToken);
                }
                else if (!string.IsNullOrWhiteSpace(GitHubToken))
                {
                    // GitHub Actions: authenticate the GHCR push with a token
                    // (the workflow token or a PAT from the ADMIN_ACCESS_TOKEN secret).
                    s = s
                        .SetProcessEnvironmentVariable(
                            "SDK_CONTAINER_REGISTRY_UNAME",
                            Environment.GetEnvironmentVariable("GITHUB_ACTOR") ?? "github-actions")
                        .SetProcessEnvironmentVariable(
                            "SDK_CONTAINER_REGISTRY_PWORD", GitHubToken);
                }
            }
            else
            {
                // No registry: write the image to a throwaway archive instead of
                // loading it into a local daemon, so no engine is needed.
                s = s.AddProperty("ContainerArchiveOutputPath", ContainerArchive);
            }

            return s;
        });
    }

    /// <summary>
    /// Returns all tags that should be applied to the generated image.
    /// </summary>
    private List<string> ContainerTags()
    {
        var cri = ContainerRuntimeIdentifier!.Value;
        var localTag = IsLocalBuild ? "local" : string.Empty;

        var genericTags = new[]
            {
                VersionFull,
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
