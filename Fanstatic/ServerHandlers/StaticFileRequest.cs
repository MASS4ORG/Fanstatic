using FolkerKinzel.MimeTypes;

namespace Fanstatic.ServerHandlers;

/// <summary>
/// Check if it is one of the Static files (serve the actual file)
/// </summary>
public class StaticFileRequest(string? basePath, bool inTheme) : IServerHandlers
{
    /// <inheritdoc />
    public bool Check(Uri requestPath)
    {
        ArgumentNullException.ThrowIfNull(requestPath);

        if (string.IsNullOrEmpty(basePath))
        {
            return false;
        }

        var fileAbsolutePath = Path.Combine(basePath, requestPath.ToString().TrimStart('/'));
        return File.Exists(fileAbsolutePath);
    }

    /// <inheritdoc />
    public async Task<string> Handle(IHttpListenerResponse response, Uri requestPath, DateTime serverStartTime)
    {
        ArgumentNullException.ThrowIfNull(requestPath);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(basePath);

        var fileAbsolutePath = Path.Combine(basePath, requestPath.ToString().TrimStart('/'));
        response.ContentType = GetContentType(fileAbsolutePath);
        await using var fileStream =
            new FileStream(fileAbsolutePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        response.ContentLength64 = fileStream.Length;
        await fileStream.CopyToAsync(response.OutputStream).ConfigureAwait(false);
        return inTheme ? "themeSt" : "static";
    }

    /// <summary>
    /// Retrieves the content type of file based on its extension.
    /// If the content type cannot be determined, the default value "application/octet-stream" is returned.
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <returns>The content type of the file.</returns>
    static string GetContentType(string filePath) => MimeString.FromFileName(filePath);
}
