using System.Net;

namespace Fanstatic.TemplateEngine;

/// <summary>
/// Renders a <see cref="TemplateError"/> as a standalone HTML page so the dev server can show it in the browser.
/// </summary>
public static class TemplateErrorOverlay
{
    /// <summary>
    /// Builds the overlay page for the given error.
    /// </summary>
    /// <param name="error">The template error to display.</param>
    /// <returns>A complete HTML document describing the failure.</returns>
    public static string Render(TemplateError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <title>Template error</title>
            <style>
            body { font-family: system-ui, sans-serif; margin: 2rem; background: #1d1f21; color: #c5c8c6; }
            h1 { font-size: 1.2rem; color: #cc6666; }
            pre { background: #282a2e; border-left: 3px solid #cc6666; padding: 1rem; overflow-x: auto; }
            </style>
            </head>
            <body>
            <h1>Template error</h1>
            <pre>{{WebUtility.HtmlEncode(error.ToString())}}</pre>
            </body>
            </html>
            """;
    }
}
