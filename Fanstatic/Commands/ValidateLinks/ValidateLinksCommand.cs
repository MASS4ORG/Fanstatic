using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Net;
using System.Text.RegularExpressions;
using Serilog;
using Fanstatic.Helpers;
using Fanstatic.Models;

namespace Fanstatic.Commands.ValidateLinks;

/// <summary>
/// Validates internal and external links in generated HTML content.
/// </summary>
public sealed class ValidateLinksCommand : BaseGeneratorCommand
{
    /// <summary>
    /// Dictionary to store pages with their failed links
    /// </summary>
    public readonly ConcurrentDictionary<IPage, List<(string Link, LinkStatus Status)>> PagesWithFailedLinks = [];

    /// <summary>
    /// Number of times to retry failed external link validations
    /// </summary>
    public int DefaultRetryCount { private get; init; } = 3;

    /// <summary>
    /// Interval in milliseconds between retry attempts
    /// </summary>
    public int DefaultRetryInterval { private get; init; } = 1000;

    /// <summary>
    /// Timeout in milliseconds for external link validation requests
    /// </summary>
    public int DefaultTimeout { private get; init; } = 10000;

    /// <summary>
    /// Link validation status flags
    /// </summary>
    [Flags]
    public enum LinkStatus
    {
        /// <summary>Link is valid and accessible</summary>
        ok = 0,

        /// <summary>Link target does not exist</summary>
        notFound = 1,

        /// <summary>Link target exists but fragment/anchor not found</summary>
        fragmentNotFound = 2,

        /// <summary>Request timed out</summary>
        timeout = 4,

        /// <summary>Other HTTP error occurred</summary>
        httpError = 8
    }

    readonly ValidateLinksOptions _settings;
    readonly IHttpClientWrapper _httpClient;
    readonly ConcurrentDictionary<string, LinkStatus> _cache = new();
    readonly FrozenDictionary<string, Regex> _ignorePatternCache;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="options">Configuration options for link validation.</param>
    /// <param name="logger">Logger instance for outputting validation results.</param>
    /// <param name="fs">File system abstraction for file operations.</param>
    /// <param name="httpClient">HTTP client wrapper for external link validation.</param>
    /// <param name="site"></param>
    public ValidateLinksCommand(
        ValidateLinksOptions options,
        ILogger logger,
        IFileSystem fs,
        IHttpClientWrapper? httpClient = null,
        ISite? site = null)
        : base(options, logger, fs, site)
    {
        _settings = options;
        _httpClient = httpClient ?? new HttpClientWrapper();
        _ignorePatternCache = BuildIgnorePatternCache(options.Ignore);
    }

    /// <summary>
    /// Parses content and validates all links
    /// </summary>
    /// <returns>The current command instance</returns>
    public async Task<ValidateLinksCommand> Parse()
    {
        ReportAllCreatedOutput();
        var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };

        await Parallel.ForEachAsync(Site.OutputReferences, options, async (kvp, _) =>
        {
            // TODO: test any type of html-like output
            if (kvp.Value is not IPage page || page.OutputFormat != "html")
            {
                return;
            }

            await ValidatePageLinks(kvp.Key, page.CompleteContent);
        });

        return this;
    }

    /// <summary>
    /// Generates validation report and returns exit code
    /// </summary>
    /// <returns>Exit code indicating success (0) or failure (1)</returns>
    public Task<int> GenerateReport()
    {
        if (PagesWithFailedLinks.Any())
        {
            foreach (var (page, failedLinks) in PagesWithFailedLinks)
            {
                var filePath = Path.Combine(Site.SourceContentPath, page.SourceRelativePath);
                // TODO: Replace for a IsSystemPage or equivalent
                if (!File.Exists(filePath))
                {
                    filePath = "";
                }

                Logger.Error("{source} ({url}) has {count} invalid links:\n{links}",
                    filePath,
                    page.Permalink,
                    failedLinks.Count,
                    string.Join("\n", failedLinks.Select(l => $"- {l.Link} ({l.Status})")));
            }

            return Task.FromResult(1);
        }

        Logger.Information("Done. No errors found!");
        return Task.FromResult(0);
    }

    /// <summary>
    /// Validates if an internal link exists in output references
    /// </summary>
    /// <param name="linkStripped">Link to validate</param>
    /// <param name="link"></param>
    /// <param name="outputReferences">Dictionary of output references</param>
    /// <returns>True if link is valid, false otherwise</returns>
    public static Task<LinkStatus> ValidateInternalLink(Uri linkStripped, Uri link,
        IDictionary<Uri, IOutput> outputReferences)
    {
        ArgumentNullException.ThrowIfNull(outputReferences);

        if (!outputReferences.TryGetValue(linkStripped, out var output))
        {
            return Task.FromResult(LinkStatus.notFound);
        }

        var fakeDomain = new Uri(new Uri("http://fakedomain.com"), link);
        var fragment = fakeDomain.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
        if (string.IsNullOrEmpty(fragment))
        {
            return Task.FromResult(LinkStatus.ok);
        }

        return Task.FromResult(output is IPage page && HtmlHelper.HasFragmentId(page.CompleteContent, fragment)
            ? LinkStatus.ok
            : LinkStatus.fragmentNotFound);
    }

    /// <summary>
    /// Validates if an external link is accessible
    /// </summary>
    /// <param name="link">Link to validate</param>
    /// <param name="httpClient">HTTP client for making requests</param>
    /// <returns>True if link is valid, false otherwise</returns>
    public async Task<LinkStatus> ValidateExternalLink(Uri link, IHttpClientWrapper httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(link);

        var linkStr = link.ToString();

        for (var i = 0; i < DefaultRetryCount; i++)
        {
            try
            {
                var status = await ValidateExternalLinkAttempt(link, linkStr, httpClient);
                if (status != LinkStatus.timeout && status != LinkStatus.httpError)
                {
                    return status;
                }
            }
            catch (OperationCanceledException)
            {
                if (i == DefaultRetryCount - 1)
                {
                    return CacheAndReturn(linkStr, LinkStatus.timeout);
                }
            }
            catch (Exception)
            {
                if (i == DefaultRetryCount - 1)
                {
                    return CacheAndReturn(linkStr, LinkStatus.httpError);
                }
            }

            Thread.Sleep(DefaultRetryInterval);
        }

        return CacheAndReturn(linkStr, LinkStatus.httpError);
    }

    async Task ValidatePageLinks(Uri pageUri, string content)
    {
        if (Site.OutputReferences[pageUri] is not IPage page)
        {
            return;
        }

        var links = HtmlHelper.ExtractLinks(content);
        var failedLinks = new List<(string Link, LinkStatus Status)>();

        foreach (var link in links)
        {
            if (IsLinkIgnored(link))
            {
                continue;
            }

            if (Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
            {
                var relativeUri = uri.IsAbsoluteUri ? uri : UrlExtension.CombineRelative(pageUri, uri);
                var (strippedUri, fullUri) = UrlExtension.CorrectRequestPath(relativeUri);

                var hostMatchesBaseUrl =
                    uri.IsAbsoluteUri
                    && (Site as ISiteOutput).BaseUrl.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase);

                // Only fall back to path-based resolution for links with a real, non-root path.
                // A bare root path ("/") coincidentally matches nearly every site's homepage, which
                // would misclassify unrelated external links (e.g. hash-routed SPAs like
                // https://matrix.to/#/!room:matrix.org, whose actual path is just "/").
                var hasNonRootPath = uri.IsAbsoluteUri && uri.AbsolutePath.Length > 1;
                var isKnownInternalPage = hasNonRootPath && Site.OutputReferences.ContainsKey(strippedUri);

                // A link is internal when it's relative, its domain matches BaseUrl, or (unless
                // strict domain matching was requested) its path already resolves to one of the
                // site's own pages regardless of which domain it was written with.
                var isInternal = !uri.IsAbsoluteUri || hostMatchesBaseUrl ||
                                 (isKnownInternalPage && !_settings.StrictBaseUrl);

                LinkStatus status;
                if (isInternal)
                {
                    var cacheKey = fullUri.ToString();
                    status = await GetOrAddAsync(cacheKey,
                        () => ValidateInternalLink(strippedUri, fullUri, Site.OutputReferences));
                }
                else if (_settings.CheckExternal)
                {
                    var cacheKey = uri.ToString();

                    status = await GetOrAddAsync(cacheKey, () => ValidateExternalLink(uri, _httpClient));
                }
                else
                {
                    continue;
                }

                if (status != LinkStatus.ok)
                {
                    failedLinks.Add((link, status));
                }
            }
        }

        if (failedLinks.Count > 0)
        {
            PagesWithFailedLinks.AddOrUpdate(page, failedLinks, (_, oldValue) =>
            {
                oldValue.AddRange(failedLinks);
                return oldValue;
            });
        }
    }

    async Task<LinkStatus> ValidateExternalLinkAttempt(Uri link, string linkStr, IHttpClientWrapper httpClient)
    {
        using var cts = new CancellationTokenSource(DefaultTimeout);
        var response = await httpClient.GetAsync(link);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            // return CacheAndReturn(linkStr, LinkStatus.HttpError);
            return CacheAndReturn(linkStr, response.StatusCode == HttpStatusCode.NotFound
                ? LinkStatus.notFound
                : LinkStatus.httpError);
        }

        var fragment = link.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);
        if (string.IsNullOrEmpty(fragment))
        {
            return CacheAndReturn(linkStr, LinkStatus.ok);
        }

        if (linkStr.Contains("#/"))
        {
            return CacheAndReturn(linkStr, LinkStatus.ok);
        }

        return await ValidateFragment(linkStr, fragment, response, cts.Token);
    }

    async Task<LinkStatus> ValidateFragment(string linkStr, string fragment, HttpResponseMessage response,
        CancellationToken token)
    {
        var content = await response.Content.ReadAsStringAsync(token);
        if (HtmlHelper.HasFragmentId(content, fragment))
        {
            return CacheAndReturn(linkStr, LinkStatus.ok);
        }

        return CacheAndReturn(linkStr, LinkStatus.fragmentNotFound);
    }

    LinkStatus CacheAndReturn(string linkStr, LinkStatus status)
    {
        _cache.TryAdd(linkStr, status);
        return status;
    }

    readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    async Task<LinkStatus> GetOrAddAsync(string key, Func<Task<LinkStatus>> factory)
    {
        if (_cache.TryGetValue(key, out var cachedStatus))
        {
            return cachedStatus;
        }

        var lockObj = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync();

        try
        {
            if (_cache.TryGetValue(key, out cachedStatus))
            {
                return cachedStatus;
            }

            var status = await factory();
            return _cache.GetOrAdd(key, status);
        }
        finally
        {
            lockObj.Release();
            _locks.TryRemove(key, out _);
        }
    }

    static FrozenDictionary<string, Regex> BuildIgnorePatternCache(IEnumerable<string> patterns)
    {
        var dict = new Dictionary<string, Regex>();
        foreach (var pattern in patterns)
        {
            if (!pattern.Contains('*') || dict.ContainsKey(pattern))
            {
                continue;
            }

            try
            {
                var regexStr = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                dict[pattern] = new Regex(regexStr, RegexOptions.Compiled);
            }
            catch
            {
                // invalid pattern — fall back to exact match in IsLinkIgnored
            }
        }

        return dict.ToFrozenDictionary();
    }

    bool IsLinkIgnored(string link)
    {
        return _settings.Ignore.Any(pattern =>
        {
            if (pattern.Contains('*'))
            {
                return _ignorePatternCache.TryGetValue(pattern, out var regex)
                    ? regex.IsMatch(link)
                    : pattern == link;
            }

            return pattern == link;
        });
    }
}
