using System.Net.Http.Headers;
using System.Net.Http.Json;
using GitHubActionsEnvironment = Nuke.Common.CI.GitHubActions.GitHubActions;

namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for integrating the GitHub Actions CI/CD.
/// </summary>
partial class Build
{
    // Setup: create a scheduled workflow in .github/workflows and give it
    // contents: write. Use the workflow token for release assets and
    // ADMIN_ACCESS_TOKEN only when tag pushes must start other workflows.
    private static GitHubActionsEnvironment GitHub => GitHubActionsEnvironment.Instance;

    [Parameter("GitHub token")]
    public readonly string GitHubToken = Environment.GetEnvironmentVariable("ADMIN_ACCESS_TOKEN") ??
                                         Environment.GetEnvironmentVariable("GITHUB_TOKEN");

    [Parameter("GitHub repository owner/name")]
    public readonly string GitHubRepository = GitHub?.Repository ??
                                              Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ??
                                              "fanstatic/fanstatic";

    [Parameter("GitHub API URL")]
    public readonly string GitHubApiBaseUrl =
        Environment.GetEnvironmentVariable("GITHUB_API_URL") ?? "https://api.github.com";

    private string GitHubPackageName => GitHubRepository.Split('/')[^1];

    /// <summary>
    /// Creates the tag and the release for the next version. Runs on a schedule
    /// (or manually): if there are new commits since the last tag, it bumps the
    /// project versions, updates the changelog, commits, pushes, then creates
    /// the tag and the GitHub release. Pushing the tag triggers the publish
    /// workflows.
    /// </summary>
    public Target GitHubCreateRelease => td => td
        .DependsOn(GitHubCreateCommit)
        .OnlyWhenStatic(() => HasNewCommits)
        .Requires(() => GitHubToken)
        .Executes(async () =>
        {
            var release = $"{TagName} / {Date}";
            var message = $"Created in {Date}";
            using var httpClient = CreateGitHubHttpClient();
            var response = await httpClient.PostAsJsonAsync(GitHubApiUrl("releases"), new
            {
                tag_name = TagName,
                target_commitish = GitTasks.GitCurrentCommit(),
                name = release,
                body = message
            }).ConfigureAwait(false);
            _ = response.EnsureSuccessStatusCode();
            Log.Information("Release {release} created with the description '{message}'",
                release, message);
        });

    /// <summary>
    /// Bumps the project versions and the changelog, then commits and pushes.
    /// </summary>
    private Target GitHubCreateCommit => td => td
        .DependsOn(CheckNewCommits, UpdateProjectVersions, UpdateChangelog)
        .OnlyWhenStatic(() => HasNewCommits)
        .Executes(() =>
        {
            GitTasks.Git("add -A");
            if (!GitTasks.Git("status --porcelain").Any())
            {
                Log.Information("No version or changelog changes to commit.");
                return;
            }

            GitTasks.Git("config --global user.name github-actions[bot]");
            GitTasks.Git("config --global user.email github-actions[bot]@users.noreply.github.com");
            GitTasks.Git($"commit -m {"chore: Automatic commit creation: " + Date + " [skip ci]"}");
            GitTasks.Git($"push origin HEAD:{GitTasks.GitCurrentBranch()}");
            Log.Information(
                "Commit in branch {branch} created with the message 'chore: Automatic commit creation: {date} [skip ci]'",
                GitTasks.GitCurrentBranch(), Date);
        });

    /// <summary>
    /// Zips the publish directory and uploads it as a release asset, one for
    /// each runtime identifier.
    /// </summary>
    public Target GitHubUploadPackage => td => td
        .DependsOn(Publish)
        .Requires(() => GitHubToken)
        .Executes(async () =>
        {
            var criName = ContainerRuntimeIdentifier is not null
                ? ContainerRuntimeIdentifier.Value.identifier
                : RuntimeIdentifier;
            var filename = $"{GitHubPackageName}-{criName}-{CurrentTag}.zip";
            var fullPath = Path.GetFullPath(filename);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            PublishDirectory.ZipTo(
                fullPath,
                filter: x => !x.HasExtension("pdb", "xml"),
                compressionLevel: CompressionLevel.Optimal,
                fileMode: FileMode.Create);

            await GitHubReleaseUpload(fullPath);
        });

    /// <summary>
    /// Pushes all images to GHCR. The actual push is done by
    /// <see cref="Build.PublishContainer"/> using the native .NET SDK container
    /// support (no Docker/Podman daemon required).
    /// </summary>
    public Target GitHubPushContainer => td => td
        .DependsOn(PublishContainer)
        .OnlyWhenStatic(() => RuntimeIdentifier != "win-x64")
        .Requires(() => GitHubToken)
        .Executes(() => { });

    /// <summary>
    /// Uploads the Windows installer as a release asset.
    /// </summary>
    public Target GitHubPushWindowsInstaller => td => td
        .DependsOn(WindowsInstaller)
        .OnlyWhenStatic(() => RuntimeIdentifier == "win-x64")
        .Requires(() => GitHubToken)
        .Executes(() => GitHubReleaseUpload(WindowsInstallerFile));

    /// <summary>
    /// Uploads the Debian package as a release asset. GitHub hosts released
    /// artifacts directly as assets, so this supersedes the GitLab generic
    /// package registry + release links.
    /// </summary>
    public Target GitHubPushDebianPackage => td => td
        .DependsOn(CreateDebianPackage)
        .Requires(() => GitHubToken)
        .Executes(() => GitHubReleaseUpload(DebianPackage));

    /// <summary>
    /// Uploads files to the release of <see cref="CurrentTag"/>.
    /// </summary>
    private async Task GitHubReleaseUpload(params string[] files)
    {
        using var httpClient = CreateGitHubHttpClient();
        var release = await httpClient.GetFromJsonAsync<GitHubRelease>(
            GitHubApiUrl($"releases/tags/{Uri.EscapeDataString(CurrentTag)}")).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"GitHub release '{CurrentTag}' was not found.");

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var existingAsset = release.assets.FirstOrDefault(asset => asset.name == fileName);
            if (existingAsset is not null)
            {
                var deleteResponse = await httpClient.DeleteAsync(
                    GitHubApiUrl($"releases/assets/{existingAsset.id}")).ConfigureAwait(false);
                _ = deleteResponse.EnsureSuccessStatusCode();
            }

            await using var fileStream = File.OpenRead(file);
            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var uploadResponse = await httpClient.PostAsync(
                $"{release.upload_url.Split('{')[0]}?name={Uri.EscapeDataString(fileName)}",
                content).ConfigureAwait(false);
            _ = uploadResponse.EnsureSuccessStatusCode();
        }

        Log.Information("Assets added to release {tag}: {files}", CurrentTag, string.Join(", ", files));
    }

    private HttpClient CreateGitHubHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GitHubToken);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("fanstatic-nuke");
        return httpClient;
    }

    private string GitHubApiUrl(string path) => $"{GitHubApiBaseUrl}/repos/{GitHubRepository}/{path}";

    private sealed record GitHubRelease(long id, string upload_url, GitHubReleaseAsset[] assets);

    private sealed record GitHubReleaseAsset(long id, string name);
}
