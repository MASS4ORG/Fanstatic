using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Nuke.Common.CI.GitLab;

namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible integrating the GitLab CI/CD.
/// </summary>
partial class Build
{
    // Setup: add GITLAB_PRIVATE_TOKEN with api scope as a masked scheduled-pipeline
    // variable, then configure the schedule in Build > Pipeline schedules. The
    // scheduled pipeline calls GitLabCreateRelease; tag pipelines publish assets.
    /// <summary>
    /// The GitLab CI/CD variables are injected by Nuke.
    /// </summary>
    private static GitLab GitLab => GitLab.Instance;

    [Parameter("GitLab private token")]
    public readonly string GitlabPrivateToken;

    [Parameter("GitLab ProjectId")]
    public readonly long GitLabProjectId = GitLab?.ProjectId ?? 0;

    [Parameter("GitLab API URL")]
    private static readonly string GitLabApiBaseUrl =
        Environment.GetEnvironmentVariable("CI_API_V4_URL");

    private static string Date =>
        DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private string PackageName => GitLab?.ProjectName ?? RegistryImage;

    /// <summary>
    /// Uploads the package to the GitLab generic package registry.
    /// One for each runtime identifier.
    /// </summary>
    /// <see href="https://docs.gitlab.com/ee/user/packages/generic_packages/"/>
    public Target GitLabUploadPackage => td => td
        .DependsOn(Publish)
        .Requires(() => GitlabPrivateToken)
        .Executes(async () =>
        {
            // The package name constructed using packageName, runtimeIdentifier, and Version
            var criName = ContainerRuntimeIdentifier is not null
                ? ContainerRuntimeIdentifier.Value.identifier
                : RuntimeIdentifier;
            var package = $"{PackageName}-{criName}-{CurrentTag}";

            // The filename of the package, constructed using the package variable
            var filename = $"{package}.zip";

            // The URL for the package in the GitLab generic package registry
            var packageLink = GitLabApiUrl($"packages/generic/{PackageName}/{CurrentTag}/{filename}");

            // Create the zip package
            var fullPath = Path.GetFullPath(filename);
            try
            {
                PublishDirectory.ZipTo(
                    fullPath,
                    filter: x => !x.HasExtension("pdb", "xml"),
                    compressionLevel: CompressionLevel.Optimal,
                    fileMode: FileMode.Create // overwrite if exists
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error zip");
                throw;
            }

            try
            {
                await using var fileStream = File.OpenRead(fullPath);
                using var httpClient = HttpClientGitLabToken();
                var response = await httpClient.PutAsync(
                    packageLink,
                    new StreamContent(fileStream)).ConfigureAwait(false);

                _ = response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "{StatusCode}: {Message}", ex.StatusCode,
                    ex.Message);
                throw;
            }

            await GitLabCreateReleaseLink(package, packageLink);
        });

    /// <summary>
    /// Creates a release in the GitLab repository.
    /// </summary>
    /// <see href="https://docs.gitlab.com/ee/api/releases/#create-a-release"/>
    public Target GitLabCreateRelease => td => td
        .DependsOn(GitLabCreateTag)
        .OnlyWhenStatic(() => HasNewCommits)
        .Requires(() => GitlabPrivateToken)
        .Executes(async () =>
        {
            try
            {
                using var httpClient = HttpClientGitLabToken();
                var message = $"Created in {Date}";
                var release = $"{TagName} / {Date}";
                var response = await httpClient.PostAsJsonAsync(
                    GitLabApiUrl("releases"),
                    new
                    {
                        tag_name = TagName,
                        name = release,
                        description = message
                    }).ConfigureAwait(false);

                _ = response.EnsureSuccessStatusCode();
                Log.Information(
                    "Release {release} created with the description '{message}'",
                    release, message);
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "{StatusCode}: {Message}", ex.StatusCode,
                    ex.Message);
                throw;
            }
        });

    /// <summary>
    /// Creates a tag in the GitLab repository.
    /// </summary>
    /// <see href="https://docs.gitlab.com/ee/api/tags.html#create-a-new-tag"/>
    private Target GitLabCreateTag => td => td
        .DependsOn(CheckNewCommits, GitLabCreateCommit)
        .OnlyWhenStatic(() => HasNewCommits)
        .Requires(() => GitlabPrivateToken)
        .Executes(async () =>
        {
            try
            {
                using var httpClient = HttpClientGitLabToken();
                var message = $"Automatic tag creation: '{TagName}' in {Date}";
                var response = await httpClient.PostAsJsonAsync(
                    GitLabApiUrl("repository/tags"),
                    new
                    {
                        tag_name = TagName,
                        @ref = GitLab?.CommitRefName ??
                               GitTasks.GitCurrentCommit(),
                        message
                    }).ConfigureAwait(false);

                _ = response.EnsureSuccessStatusCode();
                Log.Information(
                    "Tag {tag} created with the message '{message}'",
                    TagName, message);
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "{StatusCode}: {Message}", ex.StatusCode,
                    ex.Message);
                throw;
            }
        });

    /// <summary>
    /// Push all images to the registry and register them as release links.
    /// The actual push is done by <see cref="Build.PublishContainer"/> using the
    /// native .NET SDK container support (no Docker/Podman daemon required).
    /// </summary>
    public Target GitLabPushContainer => td => td
        .DependsOn(PublishContainer)
        .OnlyWhenStatic(() => RuntimeIdentifier != "win-x64")
        .Executes(async () =>
        {
            foreach (var tag in ContainerTags())
            {
                // Create a link to the GitLab release
                var tagLink =
                    GitLabApiUrl($"?orderBy=NAME&sort=asc&search[]={tag}");
                await GitLabCreateReleaseLink($"docker-{tag}", tagLink);
            }
        });

    /// <summary>
    /// Upload the Debian package to the GitLab generic package registry and
    /// attach it as a link to the release. The Debian package registry is not
    /// available on GitLab.com, so the .deb is published as a regular asset.
    /// </summary>
    /// <see href="https://docs.gitlab.com/ee/user/packages/generic_packages/"/>
    public Target GitLabPushDebianPackage => td => td
        .DependsOn(CreateDebianPackage)
        .OnlyWhenStatic(() => RuntimeToDebianArch.ContainsKey(RuntimeIdentifier))
        .Requires(() => GitlabPrivateToken)
        .Executes(async () =>
        {
            var debFilename = Path.GetFileName(DebianPackage);
            var packageLink = GitLabApiUrl($"packages/generic/{PackageName}/{CurrentTag}/{debFilename}");

            try
            {
                await using var fileStream = File.OpenRead(DebianPackage);
                using var httpClient = HttpClientGitLabToken();
                var response = await httpClient.PutAsync(
                    packageLink,
                    new StreamContent(fileStream)).ConfigureAwait(false);

                _ = response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "{StatusCode}: {Message}", ex.StatusCode,
                    ex.Message);
                throw;
            }

            await GitLabCreateReleaseLink($"debian-{DebianArch}", packageLink);
        });

    /// <summary>Uploads the Windows installer and links it from the GitLab release.</summary>
    public Target GitLabPushWindowsInstaller => td => td
        .DependsOn(WindowsInstaller)
        .OnlyWhenStatic(() => RuntimeIdentifier == "win-x64")
        .Requires(() => GitlabPrivateToken)
        .Executes(async () =>
        {
            var filename = Path.GetFileName(WindowsInstallerFile);
            var packageLink = GitLabApiUrl($"packages/generic/{PackageName}/{CurrentTag}/{filename}");
            await using var fileStream = File.OpenRead(WindowsInstallerFile);
            using var httpClient = HttpClientGitLabToken();
            var response = await httpClient.PutAsync(packageLink, new StreamContent(fileStream));
            response.EnsureSuccessStatusCode();
            await GitLabCreateReleaseLink("windows-installer", packageLink);
        });

    private Target GitLabCreateCommit => td => td
        .DependsOn(CheckNewCommits, UpdateProjectVersions, UpdateChangelog)
        .OnlyWhenStatic(() => HasNewCommits)
        .Executes(async () =>
        {
            const string ciSkip = "#ci-skip";
            var options = new JsonSerializerOptions
            {
                IncludeFields = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            // Get the list of CHANGED files, ignoring the created, moved or deleted ones
            var actions = GitTasks.Git("diff --name-only --diff-filter=M")
                .Select(x => x.Text)
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(filePath => new CommitAction
                {
                    action = CommitAction.ActionType.update.ToString(),
                    file_path = filePath,
                    content = File.ReadAllText(Path.Combine(
                        Path.GetDirectoryName(Solution.Path)!, filePath))
                })
                .ToList();

            var branch = Repository.Branch ?? GitLab?.CommitRefName;
            try
            {
                using var httpClient = HttpClientGitLabToken();
                var message =
                    $"chore: Automatic commit creation in {Date} {ciSkip}";
                var response = await httpClient.PostAsJsonAsync(
                    GitLabApiUrl("repository/commits"),
                    new
                    {
                        branch,
                        commit_message = message,
                        actions
                    }, options).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Log.Error("GitLab commit API returned {StatusCode}: {Body}",
                        (int)response.StatusCode, body);
                    response.EnsureSuccessStatusCode();
                }

                Log.Information(
                    "Commit in branch {branch} created with the message '{message}'",
                    branch, message);
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "{StatusCode}: {Message}", ex.StatusCode,
                    ex.Message);
                throw;
            }
        });

    /// <summary>
    /// Creates an HTTP client and set the authentication header.
    /// </summary>
    private HttpClient HttpClientGitLabToken()
    {
        var httpClient = new HttpClient();
        if (string.IsNullOrEmpty(GitlabPrivateToken))
        {
            httpClient.DefaultRequestHeaders.Add("JOB_TOKEN", GitLab.JobToken);
        }
        else
        {
            httpClient.DefaultRequestHeaders.Add("Private-Token",
                GitlabPrivateToken);
        }

        return httpClient;
    }

    /// <summary>
    /// Generate the GitLab API URL.
    /// </summary>
    /// <param name="url">The URL to append to the base URL.</param>
    /// <returns></returns>
    private string GitLabApiUrl(string url)
    {
        var apiUrl = $"{GitLabApiBaseUrl}/projects/{GitLabProjectId}/{url}";
        Log.Information("GitLab API call: {url}", apiUrl);
        return apiUrl;
    }

    private async Task GitLabCreateReleaseLink(string itemName, string itemLink)
    {
        try
        {
            using var httpClient = HttpClientGitLabToken();
            var response = await httpClient.PostAsJsonAsync(
                GitLabApiUrl($"releases/{TagName}/assets/links"),
                new
                {
                    name = itemName,
                    url = itemLink
                }).ConfigureAwait(false);

            _ = response.EnsureSuccessStatusCode();
            Log.Information("Link added in release {tag}: '{package}'",
                TagName, itemLink);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "{StatusCode}: {Message}", ex.StatusCode, ex.Message);
            throw;
        }
    }

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    // ReSharper disable InconsistentNaming
    internal class CommitAction
    {
        public enum ActionType
        {
            create,
            delete,
            move,
            update,
            chmod
        }

        public required string action;
        public required string file_path;
        public string previous_path;
        public string content;
        public string encoding;
        public string last_commit_id;
        public bool? execute_filemode;
    }
    // ReSharper restore InconsistentNaming
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
}
