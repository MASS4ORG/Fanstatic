namespace Fanstatic.ServerHandlers;

/// <summary>
/// Handle server requests
/// </summary>
public interface IServerHandlers
{
    /// <summary>
    /// Check if the condition is met to handle the request
    /// </summary>
    /// <param name="requestPath"></param>
    /// <returns></returns>
    bool Check(Uri requestPath);

    /// <summary>
    /// Process the request
    /// </summary>
    /// <param name="response"></param>
    /// <param name="requestPath"></param>
    /// <param name="serverStartTime"></param>
    /// <returns></returns>
    Task<string> Handle(IHttpListenerResponse response, Uri requestPath, DateTime serverStartTime);
}
