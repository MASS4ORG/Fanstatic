namespace Fanstatic.Commands.ValidateLinks;

/// <summary>
/// Default implementation of IHttpClientWrapper
/// </summary>
public class HttpClientWrapper : IHttpClientWrapper
{
    readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of HttpClientWrapper
    /// </summary>
    public HttpClientWrapper()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "C# App");
    }

    /// <summary>
    /// Sends a GET request to the specified URI
    /// </summary>
    /// <param name="uri">The URI to send the request to</param>
    /// <returns>The HTTP response message</returns>
    public Task<HttpResponseMessage> GetAsync(Uri uri)
    {
        return _httpClient.GetAsync(uri);
    }
}
