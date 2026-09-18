namespace Fanstatic.Commands.ValidateLinks;

/// <summary>
/// Wrapper interface for <see cref="HttpClient"/> to improve testability.
/// </summary>
public interface IHttpClientWrapper
{
    /// <summary>
    /// Sends a GET request to the specified URI.
    /// </summary>
    /// <param name="uri">The URI to send the request to.</param>
    /// <returns>A task representing the asynchronous operation, containing the <see cref="HttpResponseMessage"/>.</returns>
    Task<HttpResponseMessage> GetAsync(Uri uri);
}
