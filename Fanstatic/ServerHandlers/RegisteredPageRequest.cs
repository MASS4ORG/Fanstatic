using System.Reflection;
using Fanstatic.Helpers;
using Fanstatic.Models;

namespace Fanstatic.ServerHandlers;

/// <summary>
/// Return the server startup timestamp as the response
/// </summary>
public class RegisteredPageRequest : IServerHandlers
{
    readonly ISite _site;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="site"></param>
    public RegisteredPageRequest(ISite site)
    {
        _site = site;
    }

    /// <inheritdoc />
    public bool Check(Uri requestPath)
    {
        ArgumentNullException.ThrowIfNull(requestPath);

        (requestPath, var _) = UrlExtension.CorrectRequestPath(requestPath);

        if (_site.OutputReferences.TryGetValue(requestPath, out var item) && item is IPage)
            return true;

        // Cold-start: paginated URL not yet registered because parent page hasn't
        // been rendered yet (the paginate filter registers virtual pages on first render).
        // Trigger parent render to populate OutputReferences, then retry.
        var parentUrl = TryGetPaginatedParentUrl(requestPath);
        if (parentUrl is not null
            && _site.OutputReferences.TryGetValue(parentUrl, out var parent)
            && parent is IPage parentPage)
        {
            _ = parentPage.CompleteContent; // renders parent → filter fires → registers page/2, page/3…
            return _site.OutputReferences.TryGetValue(requestPath, out var registered) && registered is IPage;
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<string> Handle(IHttpListenerResponse response,
        Uri requestPath, DateTime serverStartTime)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(requestPath);

        (requestPath, var _) = UrlExtension.CorrectRequestPath(requestPath);

        if (!_site.OutputReferences.TryGetValue(requestPath, out var output) || output is not IPage page)
        {
            return "404";
        }

        var content = page.CompleteContent;
        content = InjectReloadScript(content);
        await using var writer = new StreamWriter(response.OutputStream, leaveOpen: true);
        await writer.WriteAsync(content).ConfigureAwait(false);
        return "dict";
    }

    /// <summary>
    /// If <paramref name="url"/> looks like <c>/{base}/{paginatePath}/{N}/{file}</c>
    /// (where N ≥ 2), returns the parent URL <c>/{base}/{file}</c>; otherwise null.
    /// </summary>
    Uri? TryGetPaginatedParentUrl(Uri url)
    {
        var path = url.ToString();
        var marker = "/" + _site.PaginatePath + "/";
        var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var afterMarker = path[(idx + marker.Length)..];
        var slashIdx = afterMarker.IndexOf('/');
        if (slashIdx < 0) return null;

        if (!int.TryParse(afterMarker[..slashIdx], out var pageNum) || pageNum < 2) return null;

        var filename = afterMarker[(slashIdx + 1)..];
        var basePath = path[..idx];
        return new Uri($"{basePath}/{filename}", UriKind.Relative);
    }

    /// <summary>
    /// Injects a reload script into the provided content.
    /// The script is read from a JavaScript file and injected before the closing "body" tag.
    /// </summary>
    /// <param name="content">The content to inject the reload script into.</param>
    /// <returns>The content with the reload script injected.</returns>
    static string InjectReloadScript(string content)
    {
        // Read the content of the JavaScript file
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Fanstatic.wwwroot.js.reload.js")
                           ?? throw new FileNotFoundException("Could not find the embedded JavaScript resource.");
        using var reader = new StreamReader(stream);
        var scriptContent = reader.ReadToEnd();

        // Inject the JavaScript content
        var reloadScript = $"<script>{scriptContent}</script>";

        const string bodyClosingTag = "</body>";
        content = content.Replace(bodyClosingTag, $"{reloadScript}{bodyClosingTag}", StringComparison.InvariantCulture);

        return content;
    }
}
