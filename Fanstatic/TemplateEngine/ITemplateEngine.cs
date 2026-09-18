using Fanstatic.Models;

namespace Fanstatic.TemplateEngine;

/// <summary>
/// Interface for all template engines.
/// </summary>
public interface ITemplateEngine
{
    /// <summary>
    /// Initializes the template engine for the given site.
    /// </summary>
    /// <param name="site">The site context.</param>
    void Initialize(Site site);

    /// <summary>
    /// Precompiles all templates from the theme path.
    /// </summary>
    /// <param name="themePath">The absolute theme path.</param>
    void PreCompileTheme(string themePath);

    /// <summary>
    /// Renders a template identified by key/path.
    /// </summary>
    /// <param name="templatePathOrInlineKey">Template key or path.</param>
    /// <param name="site">The site context.</param>
    /// <param name="page">The page context.</param>
    /// <param name="counter">Optional counter for resource naming scenarios.</param>
    /// <returns>The rendered template output.</returns>
    string Render(string templatePathOrInlineKey, ISite site, IPage page, int? counter = null);

    /// <summary>
    /// Renders an inline template body.
    /// </summary>
    /// <param name="templateBody">Inline template content.</param>
    /// <param name="site">The site context.</param>
    /// <param name="page">The page context.</param>
    /// <returns>The rendered template output.</returns>
    string RenderInline(string templateBody, ISite site, IPage page);

    // TODO razor: a RazorTemplateEngine would implement PreCompileTheme by
    // running Microsoft.CodeAnalysis over discovered .cshtml files, emitting
    // a delegate per template keyed on the same path the Fluid engine uses.
    // Pagination, partials, and shortcodes must all resolve through the same
    // Render(templatePathOrInlineKey, ...) entry point so the caller doesn't
    // know which engine is active.
}
