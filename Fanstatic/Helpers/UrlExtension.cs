using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Fanstatic.Helpers;

/// <summary>
/// Helper class to convert a string to a URL-friendly string.
/// </summary>
public static partial class UrlExtension
{
    [GeneratedRegex("[^a-zA-Z0-9]+")]
    private static partial Regex AlphaNumericRegex();

    [GeneratedRegex("[^a-zA-Z0-9.]+")]
    private static partial Regex AlphaNumericDotRegex();

    /// <summary>
    /// Converts a string to a URL-friendly string.
    /// It will remove all non-alphanumeric characters and replace spaces with the replacement character.
    /// </summary>
    /// <param name="title"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static string ConvertToUrlFriendly(string? title, UrlSanitizationOptions? options = null)
    {
        title ??= string.Empty;
        var effectiveOptions = options ?? new UrlSanitizationOptions();

        var cleanedTitle = !effectiveOptions.LowerCase ? title : title.ToLower(CultureInfo.CurrentCulture);

        var replacementChar = effectiveOptions.ReplacementChar ?? '\0';
        var replacementCharString = effectiveOptions.ReplacementChar.ToString() ?? string.Empty;

        // Remove non-alphanumeric characters and replace spaces with the replacement character
        cleanedTitle = (effectiveOptions.ReplaceDot ? AlphaNumericRegex() : AlphaNumericDotRegex())
            .Replace(cleanedTitle, replacementCharString)
            .Trim(replacementChar);

        return cleanedTitle;
    }

    /// <summary>
    /// Converts a path to a URL-friendly string.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static string SanitizeUrlPath(string? path, UrlSanitizationOptions? options = null)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        var hasLeadingSlash = path[0] == '/';
        var result = new StringBuilder(path.Length);

        if (hasLeadingSlash)
        {
            _ = result.Append('/');
        }

        var firstSegment = true;
        var pathSpan = path.AsSpan();
        var segmentStart = 0;

        for (var i = 0; i <= pathSpan.Length; i++)
        {
            var isSeparator = i == pathSpan.Length || pathSpan[i] == '/';
            if (!isSeparator)
            {
                continue;
            }

            var segmentLength = i - segmentStart;
            if (segmentLength > 0)
            {
                if (!firstSegment)
                {
                    _ = result.Append('/');
                }

                var segment = pathSpan.Slice(segmentStart, segmentLength).ToString();
                _ = result.Append(ConvertToUrlFriendly(segment, options));
                firstSegment = false;
            }

            segmentStart = i + 1;
        }

        return result.ToString();
    }

    /// <summary>
    /// Convert all paths to a unix path style
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string NormalizeToUnix(string? path) => (path ?? string.Empty).Replace('\\', '/');

    /// <summary>
    /// Corrects the request path by ensuring it ends with a trailing slash and appends "index.html" if necessary.
    /// The method processes both absolute and relative URIs, extracting the path, query, and fragment components,
    /// and returns a tuple containing the stripped path (without query or fragment) and the full path (with query and fragment).
    /// </summary>
    /// <param name="requestPath">The input URI to be processed. Can be absolute or relative.</param>
    /// <returns>
    /// A tuple containing two URIs:
    /// - <see cref="Uri"/> <c>stripped</c>: The corrected path without query or fragment.
    /// - <see cref="Uri"/> <c>full</c>: The corrected path with query and fragment appended (if any).
    /// </returns>
    /// <remarks>
    /// <para>
    /// If the input URI is null or empty, it is replaced with a default relative URI ("/").
    /// </para>
    /// <para>
    /// For absolute URIs, the scheme, host, and port are stripped, and only the path, query, and fragment are processed.
    /// </para>
    /// <para>
    /// For relative URIs, a fake domain ("http://fakedomain.com") is temporarily added to enable the use of
    /// <see cref="Uri.GetComponents"/> for reliable extraction of path, query, and fragment components.
    /// </para>
    /// <para>
    /// The corrected path ensures that:
    /// - It ends with a trailing slash if it does not already contain a file extension.
    /// - "index.html" is appended if the path ends with a trailing slash.
    /// </para>
    /// <para>
    /// The query and fragment components are preserved and reattached to the corrected path in their original order.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var (stripped, full) = CorrectRequestPath(new Uri("https://example.com/page#section?key=value"));
    /// // stripped: "/page/index.html"
    /// // full: "/page/index.html?key=value#section"
    /// </code>
    /// </example>
    /// <exception cref="UriFormatException">
    /// Thrown if the input URI is malformed and cannot be processed.
    /// </exception>
    public static (Uri stripped, Uri full) CorrectRequestPath(Uri requestPath)
    {
        ArgumentNullException.ThrowIfNull(requestPath);
        if (string.IsNullOrEmpty(requestPath.OriginalString))
        {
            requestPath = new Uri("/", UriKind.Relative);
        }

        // If the URI is not absolute, add a fake domain to make it absolute
        if (!requestPath.IsAbsoluteUri)
        {
            requestPath = new Uri(new Uri("http://fakedomain.com"), requestPath);
        }

        // Extract the path, query, and fragment using GetComponents
        var path = '/' + requestPath.GetComponents(UriComponents.Path, UriFormat.Unescaped);
        var query = requestPath.GetComponents(UriComponents.Query, UriFormat.Unescaped);
        var fragment = requestPath.GetComponents(UriComponents.Fragment, UriFormat.Unescaped);

        // TODO: generate a list of extensions of all pages and resources
        var knownExtensions = new[] { ".html", ".htm", ".js", ".css", ".xml" };
        var looksLikeFile = (knownExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)));

        // if (!path.EndsWith('/') && Path.GetExtension(path).Length == 0)
        if (!path.EndsWith('/') && !looksLikeFile)
        {
            path += '/';
        }

        if (path.EndsWith('/'))
        {
            path += "index.html";
        }

        // Build the full URI (path + query + fragment)
        var fullUriBuilder = new StringBuilder(path);

        if (!string.IsNullOrEmpty(query))
        {
            fullUriBuilder.Append('?').Append(query);
        }

        if (!string.IsNullOrEmpty(fragment))
        {
            fullUriBuilder.Append('#').Append(fragment);
        }

        var strippedUri = new Uri(path, UriKind.RelativeOrAbsolute);
        var fullUri = new Uri(fullUriBuilder.ToString(), UriKind.RelativeOrAbsolute);

        return (strippedUri, fullUri);
    }

    /// <summary>
    /// Combines a base URI and a relative URI into a single relative URI.
    /// </summary>
    /// <param name="basePath">The base URI. This should be a relative URI.</param>
    /// <param name="relativePath">The relative URI to combine with the base URI. This should be a relative URI.</param>
    /// <returns>A new relative URI that represents the combination of the base and relative URIs.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="basePath"/> or <paramref name="relativePath"/> is null.
    /// </exception>
    /// <example>
    /// <code>
    /// var baseUri = new Uri("/blog", UriKind.Relative);
    /// var relativeUri = new Uri("post1", UriKind.Relative);
    /// var result = UrlExtension.CombineRelative(baseUri, relativeUri); // Returns "/blog/post1"
    /// </code>
    /// </example>
    public static Uri CombineRelative(Uri basePath, Uri relativePath)
    {
        // Validate parameters for null
        ArgumentNullException.ThrowIfNull(basePath);
        ArgumentNullException.ThrowIfNull(relativePath);

        var baseStr = basePath.ToString().TrimEnd('/');
        var relativeStr = relativePath.ToString();

        if (IsAbsoluteRelativePath(relativeStr))
        {
            return new Uri(relativeStr, UriKind.Relative);
        }

        baseStr = HandleIndexHtml(baseStr);

        var resolvedPath = ResolveRelativePath(baseStr, relativeStr);

        return new Uri(resolvedPath, UriKind.Relative);
    }

    static bool IsAbsoluteRelativePath(string relativePath)
    {
        return relativePath.StartsWith("/");
    }

    static string HandleIndexHtml(string basePath)
    {
        if (basePath.EndsWith("index.html", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(basePath)?.Replace('\\', '/') ?? basePath;
        }

        return basePath;
    }

    static string ResolveRelativePath(string basePath, string relativePath)
    {
        var baseSegments = basePath.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
        var resolvedSegments = new List<string>(baseSegments.Length);
        foreach (var segment in baseSegments)
        {
            resolvedSegments.Add(segment);
        }

        var relativeSegments = relativePath.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in relativeSegments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (resolvedSegments.Count > 0)
                {
                    resolvedSegments.RemoveAt(resolvedSegments.Count - 1);
                }

                continue;
            }

            resolvedSegments.Add(segment);
        }

        return "/" + string.Join("/", resolvedSegments);
    }
}
